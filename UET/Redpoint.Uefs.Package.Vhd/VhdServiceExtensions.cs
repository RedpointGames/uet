namespace Redpoint.Uefs.Package.Vhd
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public static class VhdServiceExtensions
    {
        public static void AddUefsPackageVhd(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddSingleton<IPackageMounterFactory, VhdPackageMounterFactory>();
                services.AddSingleton<IPackageWriterFactory, VhdPackageWriterFactory>();
            }
        }
    }
}
