namespace Redpoint.ProgressMonitor
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.ProgressMonitor.Implementations;

    /// <summary>
    /// Extensions for registering progress monitoring services.
    /// </summary>
    public static class ProgressMonitorExtensions
    {
        /// <summary>
        /// Registers the <see cref="IMonitorFactory"/> and <see cref="IProgressFactory"/> services with the service collection.
        /// </summary>
        /// <param name="services">The service collection to register services with.</param>
        public static void AddProgressMonitor(this IServiceCollection services)
        {
            services.AddSingleton<IMonitorFactory, DefaultMonitorFactory>();
            services.AddSingleton<IProgressFactory, DefaultProgressFactory>();
            services.AddSingleton<IUtilities, DefaultUtilities>();
        }
    }
}
