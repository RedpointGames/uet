namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Create")]
    internal sealed class DockerCreateEndpointHandler : IEndpointHandler<DockerCreateRequest, DockerCreateResponse>
    {
        public ValueTask<DockerCreateResponse> HandleAsync(IUefsDaemon plugin, DockerCreateRequest request)
        {
            if (plugin.DockerVolumes.ContainsKey(request.Name))
            {
                throw new EndpointException<DockerCreateResponse>(409, new DockerCreateResponse
                {
                    Err = "A volume already exists with that unique name. UEFS volume names should be in the format '<unique id>:<path inside container>'."
                });
            }

            if (!request.Opts.TryGetValue("path", out string? path))
            {
                throw new EndpointException<DockerCreateResponse>(409, new DockerCreateResponse
                {
                    Err = "The volume is missing the required 'volume-opt=path=C:\\path\\inside\\container.uep' option."
                });
            }

            if (!path.StartsWith("c:\\", StringComparison.OrdinalIgnoreCase))
            {
                throw new EndpointException<DockerCreateResponse>(409, new DockerCreateResponse
                {
                    Err = "The 'path' volume option must start with 'C:\\'."
                });
            }

            plugin.DockerVolumes.Add(
                request.Name,
                new DockerVolume
                {
                    Name = request.Name,
                    FilePath = path,
                });
            return ValueTask.FromResult(new DockerCreateResponse
            {
                Err = string.Empty,
            });
        }
    }
}
