﻿namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Unmount")]
    internal class DockerUnmountEndpointHandler : IEndpointHandler<DockerUnmountRequest, DockerUnmountResponse>
    {
        public async ValueTask<DockerUnmountResponse> HandleAsync(IUefsDaemon plugin, DockerUnmountRequest request)
        {
            if (!plugin.DockerVolumes.ContainsKey(request.Name))
            {
                throw new EndpointException<DockerUnmountResponse>(404, new DockerUnmountResponse
                {
                    Err = "No such volume exists.",
                });
            }

            var volume = plugin.DockerVolumes[request.Name];
            await volume.Mutex.WaitAsync();
            try
            {
                if (volume.Mountpoint == null)
                {
                    throw new EndpointException<DockerUnmountResponse>(400, new DockerUnmountResponse
                    {
                        Err = $"This volume is not mounted.",
                    });
                }

                volume.PackageMounter?.Dispose();
                volume.PackageMounter = null;
                volume.ContainerID = null;
                volume.Mountpoint = null;

                return new DockerUnmountResponse
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
