namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Capabilities")]
    internal sealed class DockerCapabilitiesEndpointHandler : IEndpointHandler<EmptyRequest, DockerCapabilitiesResponse>
    {
        public ValueTask<DockerCapabilitiesResponse> HandleAsync(IUefsDaemon plugin, EmptyRequest request)
        {
            return ValueTask.FromResult(new DockerCapabilitiesResponse
            {
                Capabilities =
                {
                    Scope = "local",
                }
            });
        }
    }
}
