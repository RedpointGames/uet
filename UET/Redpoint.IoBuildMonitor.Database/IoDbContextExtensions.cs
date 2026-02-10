using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using System.Diagnostics.CodeAnalysis;

namespace Io.Database
{
    public static class IoDbContextExtensions
    {
        [RequiresUnreferencedCode("EF Core is not compatible with trimming.")]
        public static void AddIoDbContext(this IServiceCollection services, IHostEnvironment hostEnvironment, IConfiguration configuration)
        {
            string connectionString;
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DB_PGSQL_HOSTNAME")) || hostEnvironment.IsProduction())
            {
                connectionString = $"Host={Environment.GetEnvironmentVariable("DB_PGSQL_HOSTNAME")};Database=io;Username={Environment.GetEnvironmentVariable("DB_PGSQL_USERNAME")};Password={Environment.GetEnvironmentVariable("DB_PGSQL_PASSWORD")}";
            }
            else
            {
                connectionString = configuration.GetConnectionString("DefaultConnection")!;
            }
            services.AddDbContext<IoDbContext>(options => options
                .UseNpgsql(connectionString, ConfigureNpgsql));
        }

        internal static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder o)
        {
            o.UseNodaTime();
            o.CommandTimeout(null);
            o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        }
    }
}
