namespace UET.Services
{
    using Redpoint.Uet.BuildPipeline.Executors;
    using System.Threading.Tasks;

    internal interface IPluginVersioning
    {
        Task<(string versionName, string versionNumber)> ComputeVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            bool useStorageVirtualization,
            CancellationToken cancellationToken);
    }
}
