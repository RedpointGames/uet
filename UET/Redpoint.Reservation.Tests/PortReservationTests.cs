namespace Redpoint.Reservation.Tests
{
    using Microsoft.Extensions.DependencyInjection;

    public class PortReservationTests
    {
        [Fact]
        public async Task ReservePortWorks()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddReservation();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<IReservationManagerFactory>();

            var manager = factory.CreateLoopbackPortReservationManager();

            await using (var reservation = await manager.ReserveAsync())
            {
                Assert.Equal(127, reservation.EndPoint.Address.GetAddressBytes()[0]);
            }
        }
    }
}