namespace Redpoint.Kestrel
{
    using Microsoft.Extensions.DependencyInjection;

    public static class KestrelServiceCollectionExtensions
    {
        public static void AddKestrelFactory(this IServiceCollection services)
        {
            services.AddSingleton<IKestrelFactory, DefaultKestrelFactory>();
        }
    }
}
