namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.GrpcPipes;
    using Redpoint.Tasks;
    using System;

    internal class DefaultTaskApiWorkerPoolFactory : ITaskApiWorkerPoolFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTaskApiWorkerPoolFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITaskApiWorkerPool CreateWorkerPool(TaskApiWorkerPoolConfiguration poolConfiguration)
        {
            return new DefaultTaskApiWorkerPool(
                _serviceProvider.GetRequiredService<ILogger<DefaultTaskApiWorkerPool>>(),
                _serviceProvider.GetRequiredService<INetworkAutoDiscovery>(),
                _serviceProvider.GetRequiredService<ITaskScheduler>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                poolConfiguration);
        }
    }
}
