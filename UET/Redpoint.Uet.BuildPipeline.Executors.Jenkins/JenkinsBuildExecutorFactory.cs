namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.Workspace;

    public class JenkinsBuildExecutorFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public JenkinsBuildExecutorFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IBuildExecutor CreateExecutor(string buildServerOutputFilePath, Uri? gitUrl, string gitBranch)
        {
            return new JenkinsBuildExecutor(
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<JenkinsBuildExecutor>>(),
                buildServerOutputFilePath,
                gitUrl,
                gitBranch);
        }

        public IBuildNodeExecutor CreateNodeExecutor()
        {
            return new JenkinsBuildNodeExecutor(
                _serviceProvider,
                _serviceProvider.GetRequiredService<ILogger<JenkinsBuildNodeExecutor>>(),
                _serviceProvider.GetRequiredService<IEngineWorkspaceProvider>(),
                _serviceProvider.GetRequiredService<IDynamicWorkspaceProvider>());
        }
    }
}
