namespace Redpoint.UET.BuildPipeline.Executors.Local
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public class LocalBuildExecutorFactory : IBuildExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public LocalBuildExecutorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBuildExecutor CreateExecutor()
        {
            return _serviceProvider.GetRequiredService<LocalBuildExecutor>();
        }
    }
}
