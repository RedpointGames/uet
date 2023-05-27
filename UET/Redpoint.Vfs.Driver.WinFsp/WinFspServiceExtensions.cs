namespace Redpoint.Vfs.Driver.WinFsp
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    public static class WinFspServiceExtensions
    {
        [SupportedOSPlatform("windows6.2")]
        public static void AddWinFspVfsDriver(this IServiceCollection services)
        {
            services.AddSingleton<IVfsDriverFactory, WinFspVfsDriverFactory>();
        }
    }
}
