namespace Redpoint.OpenGE.Executor
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Executor.CompilerDb;
    using Redpoint.OpenGE.Executor.CompilerDb.PreprocessorScanner;
    using Redpoint.OpenGE.Executor.TaskExecutors;

    public static class OpenGEExecutorServiceExtensions
    {
        public static void AddOpenGEExecutor(this IServiceCollection services)
        {
            services.AddSingleton<IOpenGEGraphExecutorFactory, DefaultOpenGEGraphExecutorFactory>();
            services.AddSingleton<ICoreReservation, ProcessWideCoreReservation>();
            services.AddSingleton<IOpenGEDaemon, DefaultOpenGEDaemon>();
            services.AddSingleton<LocalTaskExecutor, LocalTaskExecutor>();
            services.AddSingleton<IOpenGETaskExecutor>(sp => sp.GetRequiredService<LocalTaskExecutor>());
            services.AddSingleton<IOpenGETaskExecutor, FileCopyTaskExecutor>();
            services.AddSingleton<IOpenGETaskExecutor, RemoteMsvcClTaskExecutor>();
            services.AddSingleton<OnDiskPreprocessorScanner, OnDiskPreprocessorScanner>();
            services.AddSingleton<IPreprocessorScanner, CachingPreprocessorScanner>();
            services.AddSingleton<ICompilerDb, InMemoryCompilerDb>();
        }
    }
}
