namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Path")]
    internal class DockerPathEndpointHandler : IEndpointHandler<DockerPathRequest, DockerPathResponse>
    {
        public ValueTask<DockerPathResponse> HandleAsync(IUefsDaemon plugin, DockerPathRequest request)
        {
            if (!plugin.DockerVolumes.ContainsKey(request.Name))
            {
                throw new EndpointException<DockerPathResponse>(404, new DockerPathResponse
                {
                    Err = "No such volume exists."
                });
            }

            var volume = plugin.DockerVolumes[request.Name];
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
