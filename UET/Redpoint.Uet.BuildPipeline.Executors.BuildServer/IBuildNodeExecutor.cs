namespace Redpoint.Uet.BuildPipeline.Executors.BuildServer
{
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IBuildNodeExecutor
    {
        /// <summary>
        /// Retrieve the "pipeline ID" from the current environment. This should be the same ID across all the build steps.
        /// </summary>
        /// <returns>The pipeline ID.</returns>
        string DiscoverPipelineId();

        /// <summary>
        /// Execute the BuildGraph node on this build server.
        /// </summary>
        /// <param name="buildSpecification">The build specification that's being built.</param>
        /// <param name="buildExecutionEvents">The interface for reporting changes in the build status.</param>
        /// <param name="nodeName">The BuildGraph node to build.</param>
        /// <param name="cancellationToken">The cancellation token if the build is cancelled.</param>
        /// <returns>The awaitable exit code.</returns>
        Task<int> ExecuteBuildNodeAsync(
            BuildSpecification buildSpecification,
            BuildConfigDynamic<BuildConfigPluginDistribution, IPrepareProvider>[]? preparePlugin,
            BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? prepareProject,
            IBuildExecutionEvents buildExecutionEvents,
            string nodeName,
            CancellationToken cancellationToken);
    }
}