namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Remove")]
    internal sealed class DockerRemoveEndpointHandler : IEndpointHandler<DockerRemoveRequest, DockerRemoveResponse>
    {
        public async ValueTask<DockerRemoveResponse> HandleAsync(IUefsDaemon plugin, DockerRemoveRequest request)
        {
            if (!plugin.DockerVolumes.ContainsKey(request.Name))
            {
                // Volume might be from a previous service run.
                return new DockerRemoveResponse
                {
                    Err = string.Empty,
                };
            }

            var volume = plugin.DockerVolumes[request.Name];
            await volume.Mutex.WaitAsync().ConfigureAwait(false);
            try
            {
                if (volume.Mountpoint != null)
                {
                    // Docker can request volumes to be deleted without 
                    // unmounting them first. Do the work of unmounting
                    // if the mountpoint still exists.
                    volume.PackageMounter?.Dispose();
                    volume.PackageMounter = null;
                    volume.ContainerID = null;
                    volume.Mountpoint = null;
                }

                plugin.DockerVolumes.Remove(request.Name);
                return new DockerRemoveResponse
                {
                    Err = string.Empty,
                };
            }
            finally
            {
                volume.Mutex.Release();
            }
        }
    }
}
