namespace Redpoint.UET.Workspace.Reservation
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.UET.Core;
    using System;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    internal class DefaultReservationManager : IReservationManager
    {
        private readonly string _rootPath;
        private readonly string _lockPath;
        private readonly ILogger<DefaultReservationManager> _logger;
        private readonly IStringUtilities _stringUtilities;
        private static ConcurrentDictionary<string, bool> _localReservations = new ConcurrentDictionary<string, bool>();


        private static void CreateDirectory(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(
                    path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute);
            }
        }

        public DefaultReservationManager(
            ILogger<DefaultReservationManager> logger,
            IStringUtilities stringUtilities)
        {
            if (OperatingSystem.IsWindows())
            {
                _rootPath = Path.Combine($"{Environment.GetEnvironmentVariable("SYSTEMDRIVE")}\\", "UES");
            }
            else if (OperatingSystem.IsMacOS())
            {
                _rootPath = "/Users/Shared/.ues";
            }
            else
            {
                _rootPath = "/tmp/.ues";
            }
            CreateDirectory(_rootPath);
            _lockPath = Path.Combine(_rootPath, ".lock");
            CreateDirectory(_lockPath);
            _logger = logger;
            _stringUtilities = stringUtilities;
        }

        private class Reservation : IReservation
        {
            private readonly SafeFileHandle _handle;
            private readonly Action _localReservationRelease;

            public string ReservedPath { get; }

            public Reservation(
                SafeFileHandle handle,
                string reservedPath,
                Action localReservationRelease)
            {
                _handle = handle;
                ReservedPath = reservedPath;
                _localReservationRelease = localReservationRelease;
            }

            public ValueTask DisposeAsync()
            {
                _localReservationRelease();
                _handle.Close();
                return ValueTask.CompletedTask;
            }
        }

        public Task<IReservation> ReserveAsync(string classification, params string[] parameters)
        {
            var id = _stringUtilities.GetStabilityHash($"{classification}:{string.Join("-", parameters)}", 14);
            for (int i = 0; i < 1000; i++)
            {
                var targetName = $"{id}-{i}";
                if (_localReservations.TryAdd(targetName, true))
                {
                    try
                    {
                        var handle = File.OpenHandle(
                            Path.Combine(_lockPath, targetName),
                            FileMode.Create,
                            FileAccess.ReadWrite,
                            FileShare.None,
                            FileOptions.DeleteOnClose);
                        var reservedPath = Path.Combine(_rootPath, targetName);
                        Directory.CreateDirectory(reservedPath);
                        _logger.LogInformation($"Reservation target '{targetName}' has been acquired in this process.");
                        return Task.FromResult<IReservation>(new Reservation(
                            handle,
                            reservedPath,
                            () =>
                            {
                                _logger.LogInformation($"Reservation target '{targetName}' has been released in this process.");
                                _localReservations.Remove(targetName, out _);
                            }));
                    }
                    catch (IOException ex) when (ex.Message.Contains("another process"))
                    {
                        // Attempt the next reservation.
                        _logger.LogInformation($"Reservation target '{targetName}' is in use by another process.");
                        _localReservations.Remove(targetName, out _);
                        continue;
                    }
                }
                else
                {
                    _logger.LogInformation($"Reservation target '{targetName}' is internally used elsewhere in this process.");
                }
            }

            throw new InvalidOperationException($"There are too many reservations for the stability ID '{id}'!");
        }
    }
}
