namespace Redpoint.Reservation.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Diagnostics;

    public class ReservationTests
    {
        [Fact]
        public async Task ReserveExactWorksForThreads()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddReservation();

            var sp = services.BuildServiceProvider();

            var factory = sp.GetRequiredService<IReservationManagerFactory>();

            var manager = factory.CreateReservationManager(Path.Combine(Path.GetTempPath(), "RedpointReservationTests"));

            var reservationId = $"test-{Environment.ProcessId}";
            for (var i = 0; i < 10; i++)
            {
                await using (var reservation1 = await manager.TryReserveExactAsync(reservationId))
                {
                    await using (var reservation2 = await manager.TryReserveExactAsync(reservationId))
                    {
                        Assert.NotNull(reservation1);
                        Assert.Null(reservation2);
                    }
                }
            }
        }
    }
}