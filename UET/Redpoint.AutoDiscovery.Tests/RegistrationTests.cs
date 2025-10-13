namespace Redpoint.AutoDiscovery.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;
    using Redpoint.Tasks;

    public class RegistrationTests
    {
        [Fact]
        public async Task TestRegistration()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoDiscovery();
            services.AddTasks();

            var sp = services.BuildServiceProvider();

            var autoDiscovery = sp.GetRequiredService<INetworkAutoDiscovery>();

            await using (var service = await autoDiscovery.RegisterServiceAsync(
                "test._discoverytest._tcp.local",
                10101,
                CancellationToken.None))
            {
            }
        }
    }
}