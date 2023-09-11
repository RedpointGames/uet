namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/Plugin.Activate")]
    internal sealed class DockerHandshakeEndpointHandler : IEndpointHandler<EmptyRequest, DockerHandshakeResponse>
    {
        public ValueTask<DockerHandshakeResponse> HandleAsync(IUefsDaemon plugin, EmptyRequest request)
        {
            return ValueTask.FromResult(new DockerHandshakeResponse
            {
                Implements = new[] { "VolumeDriver" },
            });
        }
    }
}
