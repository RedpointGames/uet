namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Microsoft.Extensions.DependencyInjection;

    public static class PackageFsServiceExtensions
    {
        public static void AddUefsPackageFs(this IServiceCollection services)
        {
            services.AddSingleton<IPackageFsFactory, DefaultPackageFsFactory>();
        }
    }
}
