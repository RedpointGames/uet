namespace Redpoint.Uet
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Services;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class UetServiceExtensions
    {
        public static void AddUet(this IServiceCollection services)
        {
            services.AddSingleton<IReleaseVersioning, DefaultReleaseVersioning>();
        }
    }
}
