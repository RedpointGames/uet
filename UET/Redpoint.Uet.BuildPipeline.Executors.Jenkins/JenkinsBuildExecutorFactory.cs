namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class JenkinsBuildExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public JenkinsBuildExecutorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBuildExecutor CreateExecutor(string buildServerOutputFilePath)
        {
            return new JenkinsBuildExecutor(
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<JenkinsBuildExecutor>>(),
                buildServerOutputFilePath);
        }
    }
}
