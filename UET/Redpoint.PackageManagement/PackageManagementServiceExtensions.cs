namespace Redpoint.PackageManagement
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Provides registration functions to register an implementation of <see cref="IPackageManager"/> into a <see cref="IServiceCollection"/>.
    /// </summary>
    public static class PathResolutionServiceExtensions
    {
        /// <summary>
        /// Add package management services (the <see cref="IPackageManager"/> service) into the service collection.
        /// </summary>
        /// <param name="services">The service collection to register an implementation of <see cref="IPackageManager"/> to.</param>
        public static void AddPackageManagement(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindows())
            {
                services.AddSingleton<IPackageManager, WinGetPackageManager>();
            }
            else if (OperatingSystem.IsMacOS())
            {
                services.AddSingleton<IPackageManager, HomebrewPackageManager>();
            }
            else
            {
                services.AddSingleton<IPackageManager, NullPackageManager>();
            }
        }
    }
}