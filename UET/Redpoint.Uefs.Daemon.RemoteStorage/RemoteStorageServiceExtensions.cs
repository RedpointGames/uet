namespace Redpoint.Uefs.Daemon.RemoteStorage
{
    using Docker.Registry.DotNet.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Daemon.RemoteStorage.Reference;
    using Redpoint.Uefs.Daemon.RemoteStorage.Registry;

    public static class RemoteStorageServiceExtensions
    {
        public static void AddUefsRemoteStorage(this IServiceCollection services)
        {
            services.AddSingleton<IRemoteStorage<RegistryReferenceInfo>, ReferenceRemoteStorage>();
            services.AddSingleton<IRemoteStorage<ManifestLayer>, RegistryRemoteStorage>();
        }
    }
}
