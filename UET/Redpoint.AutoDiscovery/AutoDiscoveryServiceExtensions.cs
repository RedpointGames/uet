namespace Redpoint.AutoDiscovery
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading.Tasks;

    public static class AutoDiscoveryServiceExtensions
    {
        public static void AddAutoDiscovery(this IServiceCollection services)
        {
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 10240))
            {
                switch (RuntimeInformation.ProcessArchitecture)
                {
                    case Architecture.X64:
                        services.AddSingleton<INetworkAutoDiscovery, Win64NetworkAutoDiscovery>();
                        break;
                    default:
                        services.AddSingleton<INetworkAutoDiscovery, UnsupportedNetworkAutoDiscovery>();
                        break;
                }
            }
            else
            {
                services.AddSingleton<INetworkAutoDiscovery, UnsupportedNetworkAutoDiscovery>();
            }
        }
    }
}
