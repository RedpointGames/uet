namespace Redpoint.Uet.Database.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Reservation;
    using Redpoint.Uet.Database.Models;
    using Xunit;

    public class DatabaseTests
    {
        [Fact]
        public async Task TestDatabaseAsync()
        {
            var services = new ServiceCollection();
            services.AddUetDatabase();
            services.AddReservation();
            services.AddLogging();
            var serviceProvider = services.BuildServiceProvider();

            if (File.Exists("uet-test.db"))
            {
                File.Delete("uet-test.db");
            }

            var dbConnectionFactory = serviceProvider.GetRequiredService<IUetDbConnectionFactory>();
            await using var connection = await dbConnectionFactory.ConnectToSpecificDatabaseFileAsync("uet-test.db", CancellationToken.None);

            {
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

                await connection.UpsertAsync(
                    new LastEnginePathModel
                    {
                        Key = "test",
                        LastEnginePath = "changedpath"
                    },
                    CancellationToken.None);

                result = await connection.FindAsync<LastEnginePathModel>("test", CancellationToken.None);
                Assert.NotNull(result);
                Assert.Equal("test", result.Key);
                Assert.Equal("changedpath", result.LastEnginePath);
            }

            {
                await connection.CreateAsync(
                    new VerifiedDllFileModel
                    {
                        Key = "test",
                        LastWriteTime = 1234
                    },
                    CancellationToken.None);

                var result = await connection.FindAsync<VerifiedDllFileModel>("test", CancellationToken.None);
                Assert.NotNull(result);
                Assert.Equal("test", result.Key);
                Assert.Equal(1234, result.LastWriteTime);

                await connection.UpsertAsync(
                    new VerifiedDllFileModel
                    {
                        Key = "test",
                        LastWriteTime = 5678
                    },
                    CancellationToken.None);

                result = await connection.FindAsync<VerifiedDllFileModel>("test", CancellationToken.None);
                Assert.NotNull(result);
                Assert.Equal("test", result.Key);
                Assert.Equal(5678, result.LastWriteTime);
            }
        }
    }
}
