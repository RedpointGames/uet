namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Docker.Registry.DotNet.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Driver;
    using System.Runtime.Versioning;

    internal class DefaultPackageFsFactory : IPackageFsFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultPackageFsFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IPackageFs CreateLocallyBackedPackageFs(string storagePath)
        {
            return new LocalPackageFs(
                _serviceProvider.GetRequiredService<ILogger<LocalPackageFs>>(),
                storagePath);
        }

        [SupportedOSPlatform("windows6.2")]
        public IPackageFs CreateVfsBackedPackageFs(string storagePath)
        {
            return new VfsPackageFs(
                _serviceProvider.GetRequiredService<IVfsDriverFactory>(),
                _serviceProvider.GetRequiredService<ILogger<IVfsLayer>>(),
                _serviceProvider.GetRequiredService<IRemoteStorage<ManifestLayer>>(),
                _serviceProvider.GetRequiredService<IRemoteStorage<RegistryReferenceInfo>>(),
                storagePath);
        }
    }
}
