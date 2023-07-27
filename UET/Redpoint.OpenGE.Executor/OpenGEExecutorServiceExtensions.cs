namespace Redpoint.OpenGE.Executor
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Executor.TaskExecutors;

    public static class OpenGEExecutorServiceExtensions
    {
        public static void AddOpenGEExecutor(this IServiceCollection services)
        {
            services.AddSingleton<IOpenGEGraphExecutorFactory, DefaultOpenGEGraphExecutorFactory>();
            services.AddSingleton<ICoreReservation, ProcessWideCoreReservation>();
            services.AddSingleton<IOpenGEDaemon, DefaultOpenGEDaemon>();
            services.AddSingleton<IOpenGETaskExecutor, LocalTaskExecutor>();
            services.AddSingleton<IOpenGETaskExecutor, FileCopyTaskExecutor>();
        }
    }
}
