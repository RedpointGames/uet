namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.GraphGenerator;
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;

    public static class DispatcherServiceExtensions
    {
        public static void AddOpenGEComponentDispatcher(this IServiceCollection services)
        {
            services.AddSingleton<ITaskDescriptorFactory, FileCopyTaskDescriptorFactory>();
            services.AddSingleton<LocalTaskDescriptorFactory, LocalTaskDescriptorFactory>();
            services.AddSingleton<ITaskDescriptorFactory>(sp => sp.GetRequiredService<LocalTaskDescriptorFactory>());
            services.AddSingleton<ITaskDescriptorFactory, RemoteMsvcClTaskDescriptorFactory>();

            services.AddSingleton<IGraphGenerator, DefaultGraphGenerator>();
            services.AddSingleton<IGraphExecutor, DefaultGraphExecutor>();

            services.AddSingleton<IDispatcherComponentFactory, DefaultDispatcherComponentFactory>();

            services.AddSingleton<IWorkerPoolFactory, DefaultWorkerPoolFactory>();

            services.AddSingleton<IToolSynchroniser, DefaultToolSynchroniser>();
        }
    }
}
