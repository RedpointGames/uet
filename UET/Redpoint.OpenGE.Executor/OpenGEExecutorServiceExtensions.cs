namespace Redpoint.OpenGE.Executor
{
    using Microsoft.Extensions.DependencyInjection;

    public static class OpenGEExecutorServiceExtensions
    {
        public static void AddOpenGEExecutor(this IServiceCollection services)
        {
            services.AddSingleton<IOpenGEExecutorFactory, DefaultOpenGEExecutorFactory>();
            services.AddSingleton<ICoreReservation, ProcessWideCoreReservation>();
            services.AddSingleton<IOpenGEDaemon, DefaultOpenGEDaemon>();
        }
    }
}
