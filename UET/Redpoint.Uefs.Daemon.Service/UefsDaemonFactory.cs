namespace Redpoint.Uefs.Daemon.Service
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.PackageStorage;
    using Redpoint.Uefs.Daemon.Service.Mounting;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;

    internal sealed class UefsDaemonFactory : IUefsDaemonFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public UefsDaemonFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IUefsDaemon CreateDaemon(string rootPath)
        {
            return new UefsDaemon(
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<UefsDaemon>>(),
                _serviceProvider.GetRequiredService<IPackageStorageFactory>(),
                _serviceProvider.GetRequiredService<IMounter<MountPackageFileRequest>>(),
                _serviceProvider.GetRequiredService<IMounter<MountGitCommitRequest>>(),
                _serviceProvider.GetRequiredService<ITransactionalDatabaseFactory>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                rootPath);
        }
    }
}
