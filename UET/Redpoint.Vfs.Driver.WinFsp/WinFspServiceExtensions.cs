namespace Redpoint.Vfs.Driver.WinFsp
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    /// <summary>
    /// Registers the <see cref="IVfsDriverFactory"/> implementation with the dependency injection service collection.
    /// </summary>
    public static class WinFspServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IVfsDriverFactory"/> implementation with the dependency injection service collection.
        /// </summary>
        /// <param name="services">The service collection to register the implementation with.</param>
        [SupportedOSPlatform("windows6.2")]
        public static void AddWinFspVfsDriver(this IServiceCollection services)
        {
            services.AddSingleton<IVfsDriverFactory, WinFspVfsDriverFactory>();
        }
    }
}
