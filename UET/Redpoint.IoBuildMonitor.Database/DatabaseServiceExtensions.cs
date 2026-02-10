namespace Io.Database
{
    using Io.Database.Utilities;
    using Microsoft.Extensions.DependencyInjection;

    public static class DatabaseServiceExtensions
    {
        public static void AddDatabaseServices(this IServiceCollection services)
        {
            services.AddSingleton<ITimestampTruncation, TimestampTruncation>();
        }
    }
}
