namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.JobXml;
    using System;

    internal class DefaultGraphExecutorFactory : IGraphExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultGraphExecutorFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IGraphExecutor CreateGraphExecutor(
            Stream xgeJobXml,
            Dictionary<string, string> environmentVariables,
            bool turnOffExtraLogInfo,
            string? buildLogPrefix)
        {
            return new DefaultGraphExecutor(
                _serviceProvider.GetRequiredService<ILogger<DefaultGraphExecutor>>(),
                _serviceProvider.GetServices<IOpenGETaskExecutor>().ToArray(),
                JobXmlReader.ParseJobXml(xgeJobXml),
                environmentVariables,
                turnOffExtraLogInfo,
                buildLogPrefix);
        }
    }
}
