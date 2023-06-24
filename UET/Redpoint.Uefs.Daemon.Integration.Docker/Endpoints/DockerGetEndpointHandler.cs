namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Get")]
    internal class DockerGetEndpointHandler : IEndpointHandler<DockerGetRequest, DockerGetResponse>
    {
        public ValueTask<DockerGetResponse> HandleAsync(IUefsDaemon plugin, DockerGetRequest request)
        {
            if (!plugin.DockerVolumes.ContainsKey(request.Name))
            {
                throw new EndpointException<DockerPathResponse>(404, new DockerPathResponse
                {
                    Err = "No such volume exists."
                });
            }

            var volume = plugin.DockerVolumes[request.Name];
            return ValueTask.FromResult(new DockerGetResponse
            {
                Err = string.Empty,
                Volume = new DockerVolumeInfo
                {
                    Name = volume.Name,
                    Mountpoint = volume.Mountpoint == null ? string.Empty : volume.Mountpoint,
                },
            });
        }
    }
}
