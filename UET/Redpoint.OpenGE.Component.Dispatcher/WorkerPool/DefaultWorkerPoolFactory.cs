namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.OpenGE.Protocol;

    internal class DefaultWorkerPoolFactory : IWorkerPoolFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultWorkerPoolFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IWorkerPool CreateWorkerPool(
            WorkerAddRequest localWorkerAddRequest)
        {
            return new DefaultWorkerPool(
                _serviceProvider.GetRequiredService<ILogger<DefaultWorkerPool>>(),
                _serviceProvider.GetRequiredService<ILogger<WorkerSubpool>>(),
                _serviceProvider.GetRequiredService<INetworkAutoDiscovery>(),
                localWorkerAddRequest);
        }
    }
}
