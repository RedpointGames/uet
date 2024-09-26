namespace Redpoint.ProcessExecution
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uba;

    /// <summary>
    /// Registers the <see cref="IUbaServerFactory"/> service with dependency injection.
    /// </summary>
    public static class UbaServiceExtensions
    {
        /// <summary>
        /// Registers the <see cref="IUbaServerFactory"/> service with dependency injection.
        /// </summary>
        public static void AddUba(this IServiceCollection services)
        {
            services.AddSingleton<IUbaServerFactory, DefaultUbaServerFactory>();
        }
    }
}
