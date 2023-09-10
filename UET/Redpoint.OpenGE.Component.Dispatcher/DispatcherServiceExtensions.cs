namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.GraphGenerator;
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;
    using Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories.Msvc;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;

    public static class DispatcherServiceExtensions
    {
        public static void AddOpenGEComponentDispatcher(this IServiceCollection services)
        {
            services.AddSingleton<ITaskDescriptorFactory, FileCopyTaskDescriptorFactory>();
            services.AddSingleton<LocalTaskDescriptorFactory, LocalTaskDescriptorFactory>();
            services.AddSingleton<ITaskDescriptorFactory>(sp => sp.GetRequiredService<LocalTaskDescriptorFactory>());

            // @note: Remote task executors are currently turned off because they don't work fully.
            //services.AddSingleton<ITaskDescriptorFactory, RemoteMsvcClTaskDescriptorFactory>();
            //services.AddSingleton<ITaskDescriptorFactory, RemoteClangTaskDescriptorFactory>();
            //services.AddSingleton<ITaskDescriptorFactory, RemoteGenericTaskDescriptorFactory>();

            services.AddSingleton<IMsvcResponseFileParser, DefaultMsvcResponseFileParser>();
            services.AddSingleton<ICommonPlatformDefines, DefaultCommonPlatformDefines>();

            services.AddSingleton<IGraphGenerator, DefaultGraphGenerator>();
            services.AddSingleton<IGraphExecutor, DefaultGraphExecutor>();

            services.AddSingleton<IStallMonitorFactory, DefaultStallMonitorFactory>();
            services.AddSingleton<IStallDiagnostics, DefaultStallDiagnostics>();

            services.AddSingleton<IDispatcherComponentFactory, DefaultDispatcherComponentFactory>();

            services.AddSingleton<ITaskApiWorkerPoolFactory, DefaultTaskApiWorkerPoolFactory>();

            services.AddSingleton<IToolSynchroniser, DefaultToolSynchroniser>();
            services.AddSingleton<IBlobSynchroniser, DefaultBlobSynchroniser>();
            services.AddSingleton<IRemoteFsManager, DefaultRemoteFsManager>();
        }
    }
}
