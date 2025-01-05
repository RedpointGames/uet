namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public class JenkinsBuildNodeExecutor : IBuildNodeExecutor
    {
        public string DiscoverPipelineId()
        {
            // TODO: This might be wrong if the ID is expected to be the same between the main executor and node executors, investigate.
            return Environment.GetEnvironmentVariable("BUILD_TAG") ?? string.Empty;
        }

        public Task<int> ExecuteBuildNodesAsync(BuildSpecification buildSpecification, BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin, BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject, IBuildExecutionEvents buildExecutionEvents, IReadOnlyList<string> nodeNames, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
