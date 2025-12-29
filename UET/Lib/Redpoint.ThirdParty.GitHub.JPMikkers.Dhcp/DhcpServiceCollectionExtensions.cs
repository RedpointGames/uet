namespace GitHub.JPMikkers.Dhcp
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class DhcpServiceCollectionExtensions
    {
        public static void AddDhcpServer(this IServiceCollection services)
        {
            services.AddSingleton<IUdpSocketFactory, DefaultUdpSocketFactory>();
            services.AddSingleton<IDhcpServerFactory, DefaultDhcpServerFactory>();
        }
    }
}
