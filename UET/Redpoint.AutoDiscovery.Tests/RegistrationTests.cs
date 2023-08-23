namespace Redpoint.AutoDiscovery.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class RegistrationTests
    {
        [Fact]
        public async Task TestRegistration()
        {
            var services = new ServiceCollection();
            services.AddAutoDiscovery();

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

    public class DiscoveryTests
    {
        [Fact]
        public async Task TestSelfDiscovery()
        {
            var services = new ServiceCollection();
            services.AddAutoDiscovery();

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