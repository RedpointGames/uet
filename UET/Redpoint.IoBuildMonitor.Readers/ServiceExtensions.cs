namespace Io.Readers
{
    using Microsoft.Extensions.DependencyInjection;

    public static class ReaderServiceExtensions
    {
        public static void AddReaders(this IServiceCollection services)
        {
            services.AddScoped<IDashboardReader, DashboardReader>();
            services.AddScoped<IHistoryReader, HistoryReader>();
            services.AddScoped<IUtilizationReader, UtilizationReader>();
            services.AddScoped<IHealthReader, HealthReader>();
        }
    }
}
