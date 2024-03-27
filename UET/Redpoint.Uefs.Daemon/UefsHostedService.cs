namespace Redpoint.Uefs.Daemon
{
    using Microsoft.Extensions.Hosting;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uet.CommonPaths;
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
            var rootPath = UetPaths.UefsRootPath;

            Directory.CreateDirectory(rootPath);

            _uefsDaemon = uefsDaemonFactory.CreateDaemon(rootPath);
            _disposed = false;
            _inited = false;
        }

        public IUefsDaemon UefsDaemon => _uefsDaemon;

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

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
