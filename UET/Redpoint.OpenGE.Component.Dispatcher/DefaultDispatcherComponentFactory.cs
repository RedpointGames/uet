namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.GraphGenerator;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;

    internal class DefaultDispatcherComponentFactory : IDispatcherComponentFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultDispatcherComponentFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IDispatcherComponent Create(
            ITaskApiWorkerPool workerPool,
            string? pipeName)
        {
            return new DefaultDispatcherComponent(
                _serviceProvider.GetRequiredService<ILogger<DefaultDispatcherComponent>>(),
                _serviceProvider.GetRequiredService<IGraphGenerator>(),
                _serviceProvider.GetRequiredService<IGraphExecutor>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                workerPool,
                pipeName);
        }
    }
}
