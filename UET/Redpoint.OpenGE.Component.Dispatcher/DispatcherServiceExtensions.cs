namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.OpenGE.Component.Dispatcher.GraphGenerator;
    using Redpoint.OpenGE.Component.Dispatcher.TaskDescriptorFactories;

    public static class DispatcherServiceExtensions
    {
        public static void AddOpenGEComponentDispatcher(this IServiceCollection services)
        {
            services.AddSingleton<ITaskDescriptorFactory, FileCopyTaskDescriptorFactory>();
            services.AddSingleton<ITaskDescriptorFactory, LocalTaskDescriptorFactory>();
            services.AddSingleton<ITaskDescriptorFactory, RemoteMsvcClTaskDescriptorFactory>();

            services.AddSingleton<IGraphGenerator, DefaultGraphGenerator>();

            services.AddSingleton<IDispatcherComponentFactory, DefaultDispatcherComponentFactory>();
        }
    }
}
