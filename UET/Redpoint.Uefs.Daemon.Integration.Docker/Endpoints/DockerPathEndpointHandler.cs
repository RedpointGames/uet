namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Path")]
    internal sealed class DockerPathEndpointHandler : IEndpointHandler<DockerPathRequest, DockerPathResponse>
    {
        public ValueTask<DockerPathResponse> HandleAsync(IUefsDaemon plugin, DockerPathRequest request)
        {
            if (!plugin.DockerVolumes.TryGetValue(request.Name, out DockerVolume? volume))
            {
                throw new EndpointException<DockerPathResponse>(404, new DockerPathResponse
                {
                    Err = "No such volume exists."
                });
            }

            if (volume.Mountpoint == null)
            {
                throw new EndpointException<DockerPathResponse>(400, new DockerPathResponse
                {
                    Err = $"This volume is not mounted by any container."
                });
            }

            return ValueTask.FromResult(new DockerPathResponse
            {
                Mountpoint = volume.Mountpoint,
                Err = string.Empty,
            });
        }
    }
}
