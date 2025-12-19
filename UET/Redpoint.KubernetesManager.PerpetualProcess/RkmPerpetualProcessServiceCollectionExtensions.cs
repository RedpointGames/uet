namespace Redpoint.KubernetesManager.PerpetualProcess
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class RkmPerpetualProcessServiceCollectionExtensions
    {
        public static void AddRkmPerpetualProcess(this IServiceCollection services)
        {
            services.AddSingleton<IProcessMonitorFactory, DefaultProcessMonitorFactory>();
            services.AddSingleton<IProcessKiller, DefaultProcessKiller>();
        }
    }
}
