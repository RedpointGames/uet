namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;

    internal class DefaultDispatcherComponentFactory : IDispatcherComponentFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultDispatcherComponentFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IDispatcherComponent Create(string? pipeName)
        {
            return new DefaultDispatcherComponent(
                _serviceProvider.GetRequiredService<ILogger<DefaultDispatcherComponent>>(),
                _serviceProvider.GetRequiredService<IGraphExecutorFactory>(),
                _serviceProvider.GetRequiredService<IGrpcPipeFactory>(),
                pipeName);
        }
    }
}
