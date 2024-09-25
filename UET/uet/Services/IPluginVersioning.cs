namespace UET.Services
{
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Plugin;
    using System.Threading.Tasks;

    internal interface IPluginVersioning
    {
        Task<(string versionName, string versionNumber)> ComputeVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            BuildConfigPluginPackageType versioningType,
            bool useStorageVirtualization,
            CancellationToken cancellationToken);
    }
}
