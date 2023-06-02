namespace Redpoint.OpenGE.Executor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Executor.BuildSetData;
    using Redpoint.ProcessExecution;
    using System;

    internal class DefaultOpenGEExecutorFactory : IOpenGEExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultOpenGEExecutorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IOpenGEExecutor CreateExecutor(Stream xgeJobXml, bool turnOffExtraLogInfo, string? buildLogPrefix)
        {
            return new DefaultOpenGEExecutor(
                _serviceProvider.GetRequiredService<ILogger<DefaultOpenGEExecutor>>(),
                _serviceProvider.GetRequiredService<ICoreReservation>(),
                _serviceProvider.GetRequiredService<IProcessExecutor>(),
                BuildSetReader.ParseBuildSet(xgeJobXml),
                turnOffExtraLogInfo,
                buildLogPrefix);
        }
    }
}
