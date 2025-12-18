namespace Redpoint.Uet
{
    using Microsoft.Extensions.DependencyInjection;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Services;

    public static class UetServiceExtensions
    {
        public static void AddUet(this IServiceCollection services)
        {
            services.AddSingleton<IReleaseVersioning, DefaultReleaseVersioning>();
        }
    }
}
