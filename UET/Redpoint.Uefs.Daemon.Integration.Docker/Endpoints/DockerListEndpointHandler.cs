namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.LegacyModels;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.List")]
    internal sealed class DockerListEndpointHandler : IEndpointHandler<EmptyRequest, DockerListResponse>
    {
        public ValueTask<DockerListResponse> HandleAsync(IUefsDaemon plugin, EmptyRequest request)
        {
            return ValueTask.FromResult(new DockerListResponse
            {
                Err = string.Empty,
                Volumes = plugin.DockerVolumes.Values.Select(x => new DockerVolumeInfo
                {
                    Name = x.Name,
                    Mountpoint = x.Mountpoint == null ? string.Empty : x.Mountpoint,
                }).ToArray()
            });
        }
    }
}
