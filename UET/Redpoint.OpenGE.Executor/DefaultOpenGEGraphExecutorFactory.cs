namespace Redpoint.OpenGE.Executor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Executor.BuildSetData;
    using System;

    internal class DefaultOpenGEGraphExecutorFactory : IOpenGEGraphExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultOpenGEGraphExecutorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IOpenGEGraphExecutor CreateGraphExecutor(Stream xgeJobXml, bool turnOffExtraLogInfo, string? buildLogPrefix)
        {
            return new DefaultOpenGEGraphExecutor(
                _serviceProvider.GetRequiredService<ILogger<DefaultOpenGEGraphExecutor>>(),
                _serviceProvider.GetServices<IOpenGETaskExecutor>().ToArray(),
                BuildSetReader.ParseBuildSet(xgeJobXml),
                turnOffExtraLogInfo,
                buildLogPrefix);
        }
    }
}
