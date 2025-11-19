namespace Redpoint.Uet.Database
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Hashing;
    using Redpoint.Reservation;
    using Redpoint.Uet.Database.Migrations;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Reservation;
    using SQLitePCL;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO.Hashing;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;
    using Mutex = Concurrency.Mutex;

    internal class DefaultUetDbConnectionFactory : IUetDbConnectionFactory, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DefaultUetDbConnectionFactory> _logger;
        private readonly IReservationManagerForUet? _reservationManagerForUet;
        private readonly IReservationManager _reservationManagerForNativeLibrary;
        private IReservation? _nativeLibraryReservation;

        private static Mutex _nativeLibraryMutex = new();
        private static bool _isNativeLibraryInitialized = false;

        public DefaultUetDbConnectionFactory(
            IServiceProvider serviceProvider,
            IReservationManagerFactory reservationManagerFactory,
            ILogger<DefaultUetDbConnectionFactory> logger,
            IReservationManagerForUet? reservationManagerForUet = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _reservationManagerForUet = reservationManagerForUet;
            _reservationManagerForNativeLibrary = _reservationManagerForUet ?? reservationManagerFactory.CreateReservationManager(Path.GetTempPath());
            _nativeLibraryReservation = null;
        }

        private async Task InitializeNativeLibraryIfNeeded()
        {
            if (_isNativeLibraryInitialized)
            {
                return;
            }

            using (await _nativeLibraryMutex.WaitAsync(CancellationToken.None))
            {
                if (_isNativeLibraryInitialized)
                {
                    return;
                }

                string embeddedResourceName;
                string libraryName;
                if (OperatingSystem.IsWindows())
                {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    {
                        embeddedResourceName = "sqlite.win-x64";
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("Unsupported architecture for Redpoint.Uet.Database!");
                    }
                    libraryName = "e_sqlite3.dll";
                }
                else if (OperatingSystem.IsMacOS())
                {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
                    {
                        embeddedResourceName = "sqlite.osx-arm64";
                    }
                    else if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    {
                        embeddedResourceName = "sqlite.osx-x64";
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("Unsupported architecture for Redpoint.Uet.Database!");
                    }
                    libraryName = "libe_sqlite3.dylib";
                }
                else if (OperatingSystem.IsLinux())
                {
                    if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
                    {
                        embeddedResourceName = "sqlite.linux-x64";
                    }
                    else if (RuntimeInformation.ProcessArchitecture == Architecture.X86)
                    {
                        embeddedResourceName = "sqlite.linux-x86";
                    }
                    else
                    {
                        throw new PlatformNotSupportedException("Unsupported architecture for Redpoint.Uet.Database!");
                    }
                    libraryName = "libe_sqlite3.so";
                }
                else
                {
                    throw new PlatformNotSupportedException("Unsupported platform for Redpoint.Uet.Database!");
                }

                string reservationParameter;
                using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedResourceName)!)
                {
                    var hasher = new XxHash64();
                    await hasher.AppendAsync(stream, CancellationToken.None).ConfigureAwait(false);
                    reservationParameter = BitConverter.ToInt64(hasher.GetCurrentHash()).ToString(CultureInfo.InvariantCulture);
                }

                _nativeLibraryReservation = await _reservationManagerForNativeLibrary
                    .ReserveAsync("SqliteNativeLibrary", reservationParameter)
                    .ConfigureAwait(false);

                var temporaryPath = Path.Combine(_nativeLibraryReservation.ReservedPath, libraryName + ".tmp");
                var desiredPath = Path.Combine(_nativeLibraryReservation.ReservedPath, libraryName);

                // Extract file if it doesn't already exist.
                if (!File.Exists(desiredPath))
                {
                    using (var fileStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedResourceName)!)
                        {
                            await stream.CopyToAsync(fileStream).ConfigureAwait(false);
                        }
                    }
                    File.Move(temporaryPath, desiredPath);
                    _logger.LogInformation($"Extracted Sqlite3 runtime library to: {desiredPath}");
                }
                else
                {
                    _logger.LogInformation($"Loaded existing Sqlite3 runtime library from: {desiredPath}");
                }

                // Load the native library.
                {
                    NativeLibrary.SetDllImportResolver(
                        typeof(SQLite3Provider_e_sqlite3).Assembly,
                        (string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
                        {
                            if (libraryName.Contains("e_sqlite3", StringComparison.InvariantCultureIgnoreCase))
                            {
                                return NativeLibrary.Load(desiredPath);
                            }
                            else
                            {
                                return IntPtr.Zero;
                            }
                        });
                    raw.SetProvider(new SQLite3Provider_e_sqlite3());
                }

                _isNativeLibraryInitialized = true;
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_nativeLibraryReservation != null)
            {
                await _nativeLibraryReservation.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async Task<IUetDbConnection> ConnectToDefaultDatabaseAsync(CancellationToken cancellationToken)
        {
            if (_reservationManagerForUet == null)
            {
                throw new InvalidOperationException("Can't call ConnectToDefaultDatabaseAsync if the IReservationManagerForUet service is not available!");
            }

            await InitializeNativeLibraryIfNeeded();

            var readyToReturn = false;
            var reservation = await _reservationManagerForUet.ReserveExactAsync("UetDatabase", cancellationToken, hold: true);
            try
            {
                var connection = new DefaultUetDbConnection(
                    _serviceProvider.GetServices<IMigration>(),
                    Path.Combine(reservation.ReservedPath, "uet.db"));
                connection.Reservation = reservation;
                await connection.ConnectAsync(cancellationToken);
                readyToReturn = true;
                return connection;
            }
            finally
            {
                if (!readyToReturn)
                {
                    await reservation.DisposeAsync();
                }
            }
        }

        public async Task<IUetDbConnection> ConnectToSpecificDatabaseFileAsync(string databasePath, CancellationToken cancellationToken)
        {
            await InitializeNativeLibraryIfNeeded();

            var connection = new DefaultUetDbConnection(
                _serviceProvider.GetServices<IMigration>(),
                databasePath);
            await connection.ConnectAsync(cancellationToken);
            return connection;
        }
    }
}
