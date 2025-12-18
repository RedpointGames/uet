namespace Redpoint.Uet.Services
{
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Threading.Tasks;

    public interface IReleaseVersioning
    {
        Task<(string versionName, string versionNumber)> ComputePluginVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            BuildConfigPluginPackageType versioningType,
            CancellationToken cancellationToken);

        string ComputeProjectReleaseVersion();
    }
}
