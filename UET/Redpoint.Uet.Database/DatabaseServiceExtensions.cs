namespace Redpoint.Uet.Database
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Database.Migrations;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public static class DatabaseServiceExtensions
    {
        public static void AddUetDatabase(this IServiceCollection services)
        {
            services.AddSingleton<IUetDbConnectionFactory, DefaultUetDbConnectionFactory>();

            services.AddSingleton<IMigration, Migration001_LastEnginePath>();
        }
    }
}
