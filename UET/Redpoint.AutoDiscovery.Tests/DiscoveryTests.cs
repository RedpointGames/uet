namespace Redpoint.AutoDiscovery.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Tasks;
    using Xunit;

    public class DiscoveryTests
    {
        [Fact(Skip = "This test is unreliable.")]
        public async Task TestSelfDiscovery()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAutoDiscovery();
            services.AddTasks();

            var sp = services.BuildServiceProvider();

            var autoDiscovery = sp.GetRequiredService<INetworkAutoDiscovery>();

            var entries = new List<NetworkService>();

            await using (var service = await autoDiscovery.RegisterServiceAsync(
                "test._discoverytest._tcp.local",
                10101,
                CancellationToken.None))
            {
                await foreach (var entry in autoDiscovery.DiscoverServicesAsync(
                    "_discoverytest._tcp.local",
                    new CancellationTokenSource(2500).Token))
                {
                    entries.Add(entry);
                }
            }

            Assert.NotEmpty(entries);
        }
    }
}