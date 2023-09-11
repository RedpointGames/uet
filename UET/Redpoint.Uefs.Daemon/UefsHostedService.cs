namespace Redpoint.Uefs.Daemon
{
    using Microsoft.Extensions.Hosting;
    using Redpoint.Uefs.Daemon.Abstractions;
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class UefsHostedService : IHostedService
    {
        private readonly IUefsDaemon _uefsDaemon;
        private bool _disposed;
        private bool _inited;

        public UefsHostedService(
            IUefsDaemonFactory uefsDaemonFactory)
        {
            string rootPath;
            if (OperatingSystem.IsWindows())
            {
                rootPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "UEFS");
            }
            else if (OperatingSystem.IsMacOS())
            {
                rootPath = Path.Combine("/Users", "Shared", "UEFS");
            }
            else
            {
                throw new PlatformNotSupportedException();
            }

            Directory.CreateDirectory(rootPath);

            _uefsDaemon = uefsDaemonFactory.CreateDaemon(rootPath);
            _disposed = false;
            _inited = false;
        }

        public IUefsDaemon UefsDaemon => _uefsDaemon;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(IUefsDaemon));
            }

            if (_inited)
            {
                throw new InvalidOperationException("UEFS already initialized.");
            }

            _inited = true;
            await _uefsDaemon.StartAsync().ConfigureAwait(false);
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _disposed = true;
            await _uefsDaemon.DisposeAsync().ConfigureAwait(false);
        }
    }
}
