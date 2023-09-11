namespace Redpoint.OpenGE.Agent
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Component.PreprocessorCache.OnDemand;
    using Redpoint.OpenGE.Component.Worker;

    internal sealed class DefaultOpenGEAgentFactory : IOpenGEAgentFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultOpenGEAgentFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IOpenGEAgent CreateAgent(
            bool runPreprocessorComponent,
            bool runLocalWorker)
        {
            return new DefaultOpenGEAgent(
                _serviceProvider.GetRequiredService<ILogger<DefaultOpenGEAgent>>(),
                _serviceProvider.GetRequiredService<IDispatcherComponentFactory>(),
                _serviceProvider.GetRequiredService<IWorkerComponentFactory>(),
                _serviceProvider.GetRequiredService<ITaskApiWorkerPoolFactory>(),
                _serviceProvider.GetRequiredService<IPreprocessorCacheFactory>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                runPreprocessorComponent,
                runLocalWorker);
        }
    }
}