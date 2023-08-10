namespace Redpoint.OpenGE.Core
{
    using Microsoft.Extensions.DependencyInjection;

    public static class CoreServiceExtensions
    {
        public static void AddOpenGECore(this IServiceCollection services)
        {
            services.AddSingleton<IReservationManagerForOpenGE, ReservationManagerForOpenGE>();
        }
    }
}
