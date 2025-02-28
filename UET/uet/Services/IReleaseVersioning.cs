namespace UET.Services
{
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Threading.Tasks;

    internal interface IReleaseVersioning
    {
        Task<(string versionName, string versionNumber)> ComputePluginVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            BuildConfigPluginPackageType versioningType,
            CancellationToken cancellationToken);

        string ComputeProjectReleaseVersion();
    }
}
