namespace Redpoint.Uefs.Daemon.PackageStorage
{
    using Microsoft.Extensions.DependencyInjection;

    public static class PackageStorageServiceExtensions
    {
        public static void AddUefsPackageStorage(this IServiceCollection services)
        {
            services.AddSingleton<IPackageStorageFactory, DefaultPackageStorageFactory>();
        }
    }
}
