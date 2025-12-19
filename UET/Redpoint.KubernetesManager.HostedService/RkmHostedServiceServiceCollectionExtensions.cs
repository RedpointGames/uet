namespace Redpoint.KubernetesManager.HostedService
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Commands.Cluster;
    using UET.Commands.Internal.Rkm;

    public static class RkmHostedServiceServiceCollectionExtensions
    {
        public static void AddRkmHostedServiceEnvironment(this IServiceCollection services, string applicationName)
        {
            services.AddSingleton<IHostApplicationLifetime>(sp => sp.GetRequiredService<RkmHostApplicationLifetime>());
            services.AddSingleton<RkmHostApplicationLifetime, RkmHostApplicationLifetime>();
            services.AddSingleton<IHostEnvironment>(sp => new RkmHostEnvironment(applicationName));
            services.AddSingleton<IHostedServiceFromExecutable, DefaultHostedServiceFromExecutable>();
        }
    }
}
