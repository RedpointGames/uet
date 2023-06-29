namespace Redpoint.Uet.BuildPipeline.Executors
{
    using System.Threading.Tasks;

    public interface ISdkSetupForBuildExecutor
    {
        Task<Dictionary<string, string>> SetupForBuildAsync(
            BuildSpecification buildSpecification,
            string nodeName,
            string enginePath,
            Dictionary<string, string> inputEnvironmentVariables,
            CancellationToken cancellationToken);
    }
}
