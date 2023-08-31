namespace Redpoint.OpenGE.Component.Worker
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using System;

    internal class DefaultWorkerComponentFactory : IWorkerComponentFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultWorkerComponentFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IWorkerComponent Create(bool localUseOnly)
        {
            return new DefaultWorkerComponent(
                _serviceProvider.GetRequiredService<IToolManager>(),
                _serviceProvider.GetRequiredService<IBlobManager>(),
                _serviceProvider.GetRequiredService<IExecutionManager>(),
                _serviceProvider.GetRequiredService<ILogger<DefaultWorkerComponent>>(),
                _serviceProvider.GetRequiredService<INetworkAutoDiscovery>(),
                localUseOnly);
        }
    }
}
