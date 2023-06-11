using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Redpoint.UET.UAT.Tests")]

namespace Redpoint.UET.UAT
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.UAT.Internal;

    public static class UETUATServiceExtensions
    {
        public static void AddUETUAT(this IServiceCollection services)
        {
            services.AddSingleton<IBuildConfigurationManager, DefaultBuildConfigurationManager>();
            if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
            {
                services.AddSingleton<ILocalHandleCloser, NativeLocalHandleCloser>();
            }
            else
            {
                services.AddSingleton<ILocalHandleCloser, NullLocalHandleCloser>();
            }
            services.AddSingleton<IRemoteHandleCloser, DefaultRemoteHandleCloser>();

            services.AddSingleton<IUATExecutor, DefaultUATExecutor>();
        }
    }
}