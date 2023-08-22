namespace Redpoint.AutoDiscovery.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;

    public class RegistrationTests
    {
        [Fact]
        public async void TestRegistration()
        {
            var services = new ServiceCollection();
            services.AddAutoDiscovery();

            var sp = services.BuildServiceProvider();

            var autoDiscovery = sp.GetRequiredService<INetworkAutoDiscovery>();

            await using (var service = await autoDiscovery.RegisterServiceAsync(
                "register-test._grpc._tcp.local",
                10101,
                CancellationToken.None))
            {
            }
        }
    }
}