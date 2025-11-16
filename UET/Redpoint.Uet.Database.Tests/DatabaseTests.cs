namespace Redpoint.Uet.Database.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.Database.Models;
    using Xunit;

    public class DatabaseTests
    {
        [Fact]
        public async Task TestMigrationsAsync()
        {
            var services = new ServiceCollection();
            services.AddUetDatabase();
            var serviceProvider = services.BuildServiceProvider();

            if (File.Exists("uet-test.db"))
            {
                File.Delete("uet-test.db");
            }

            var dbConnectionFactory = serviceProvider.GetRequiredService<IUetDbConnectionFactory>();
            await using var connection = await dbConnectionFactory.ConnectToSpecificDatabaseFileAsync("uet-test.db", CancellationToken.None);

            await connection.CreateAsync(
                new LastEnginePathModel
                {
                    Key = "test",
                    LastEnginePath = "some\\path"
                },
                CancellationToken.None);

            var result = await connection.FindAsync<LastEnginePathModel>("test", CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal("test", result.Key);
            Assert.Equal("some\\path", result.LastEnginePath);
        }
    }
}
