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
            if (!plugin.DockerVolumes.TryGetValue(request.Name, out DockerVolume? volume))
            {
                // Volume might be from a previous service run.
                return new DockerRemoveResponse
                {
                    Err = string.Empty,
                };
            }

            await volume.Mutex.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (volume.Mountpoint != null)
                {
                    // Docker can request volumes to be deleted without 
                    // unmounting them first. Do the work of unmounting
                    // if the mountpoint still exists.
                    if (volume.PackageMounter != null)
                    {
                        await volume.PackageMounter.DisposeAsync().ConfigureAwait(false);
                    }
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
