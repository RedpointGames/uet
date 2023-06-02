namespace UET.Services
{
    using Grpc.Core.Logging;
    using Redpoint.UET.BuildPipeline.Executors;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal interface IPluginVersioning
    {
        Task<(string versionName, string versionNumber)> ComputeVersionNameAndNumberAsync(
            BuildEngineSpecification engineSpec,
            bool useStorageVirtualization,
            CancellationToken cancellationToken);
    }
}
