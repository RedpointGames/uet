namespace Io.Database
{
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;

    public class ApplyDbMigrationsHostedService : IHostedService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ApplyDbMigrationsHostedService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplyDbMigrationsHostedService>>();

                // Apply migrations.
                logger.LogInformation("Applying migrations...");
                var context = scope.ServiceProvider.GetRequiredService<IoDbContext>();
                await context.Database.MigrateAsync(cancellationToken: cancellationToken);

                // Update functions and procedures.
                foreach (var name in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(x => x.StartsWith("Io.Database.Procedures", StringComparison.InvariantCulture) && x.EndsWith(".sql", StringComparison.InvariantCulture)))
                {
                    logger.LogInformation($"Updating function/procedure '{name}'...");
                    using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(name)!))
                    {
                        await context.Database.ExecuteSqlRawAsync(await reader.ReadToEndAsync(cancellationToken), cancellationToken: cancellationToken);
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
