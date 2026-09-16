using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.Uet.Uat.Tests")]

namespace Redpoint.Uet.Uat
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Uat.Internal;

    public static class UetUatServiceExtensions
    {
        public static void AddUetUat(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddSingleton<ILocalHandleCloser, NativeLocalHandleCloser>();
            }
            else
            {
                services.AddSingleton<ILocalHandleCloser, NullLocalHandleCloser>();
            }
            services.AddSingleton<IRemoteHandleCloser, DefaultRemoteHandleCloser>();

            services.AddSingleton<IUatExecutor, DefaultUatExecutor>();
            services.AddSingleton<IUatStartupHookProvider, DefaultUatStartupHookProvider>();
        }
    }
}