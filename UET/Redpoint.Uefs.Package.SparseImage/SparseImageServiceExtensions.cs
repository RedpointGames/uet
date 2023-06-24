using Microsoft.Extensions.DependencyInjection;

namespace Redpoint.Uefs.Package.SparseImage
{
    public static class SparseImageServiceExtensions
    {
        public static void AddUefsPackageSparseImage(this IServiceCollection services)
        {
            if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IPackageMounterFactory, SparseImagePackageMounterFactory>();
                services.AddSingleton<IPackageWriterFactory, SparseImagePackageWriterFactory>();
            }
        }
    }
}
