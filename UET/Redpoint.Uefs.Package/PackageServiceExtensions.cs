using Microsoft.Extensions.DependencyInjection;

namespace Redpoint.Uefs.Package
{
    public static class PackageServiceExtensions
    {
        public static void AddUefsPackage(this IServiceCollection services)
        {
            services.AddSingleton<IPackageManifestAssembler, DefaultPackageManifestAssembler>();
            services.AddSingleton<IPackageManifestDataWriter, DefaultPackageManifestDataWriter>();
            services.AddSingleton<IPackageMounterDetector, DefaultPackageMounterDetector>();
        }
    }
}
