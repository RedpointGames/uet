namespace Redpoint.Reservation
{
    using Microsoft.Extensions.DependencyInjection;

    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> that registers that
    /// reservation manager services.
    /// </summary>
    public static class ReservationServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IReservationManagerFactory"/> service with the given
        /// service collection.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        public static void AddReservation(this IServiceCollection services)
        {
            services.AddSingleton<IReservationManagerFactory, DefaultReservationManagerFactory>();
        }
    }
}
