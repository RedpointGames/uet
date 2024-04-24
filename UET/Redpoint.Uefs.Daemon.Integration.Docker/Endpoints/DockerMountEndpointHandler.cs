namespace Redpoint.Uefs.Daemon.Integration.Docker.Endpoints
{
    using global::Docker.DotNet;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Integration.Docker;
    using Redpoint.Uefs.Daemon.Integration.Docker.Models;
    using Redpoint.Uefs.Package;
    using Redpoint.Uefs.Protocol;
    using System.Runtime.Versioning;
    using System.Security.Cryptography;
    using System.Security.Principal;
    using System.Text;
    using System.Text.Json;

    [SupportedOSPlatform("windows")]
    [Endpoint("/VolumeDriver.Mount")]
    internal sealed class DockerMountEndpointHandler : IEndpointHandler<DockerMountRequest, DockerMountResponse>
    {
        private readonly IPackageMounterDetector _packageMounterDetector;
        private readonly ILogger<DockerMountEndpointHandler> _logger;

        public DockerMountEndpointHandler(
            IPackageMounterDetector packageMounterDetector,
            ILogger<DockerMountEndpointHandler> logger)
        {
            _packageMounterDetector = packageMounterDetector;
            _logger = logger;
        }

        public async ValueTask<DockerMountResponse> HandleAsync(IUefsDaemon plugin, DockerMountRequest request)
        {
            if (!plugin.DockerVolumes.TryGetValue(request.Name, out DockerVolume? volume))
            {
                throw new EndpointException<DockerMountResponse>(404, new DockerMountResponse
                {
                    Err = "No such volume exists.",
                    Mountpoint = string.Empty,
                });
            }

            await volume.Mutex.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (volume.Mountpoint != null)
                {
                    throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                    {
                        Err = $"This volume is already mounted by container '{volume.ContainerID}'.",
                        Mountpoint = string.Empty,
                    });
                }

                if (volume.PackageMounter != null)
                {
                    throw new EndpointException<DockerMountResponse>(500, new DockerMountResponse
                    {
                        Err = $"Internal error: This volume still has a mount associated with it.",
                        Mountpoint = string.Empty,
                    });
                }

                string hash = Hash.Sha1AsHexString(volume.Name, Encoding.UTF8);

                try
                {
                    // request.ID is auto-generated, and isn't the ID of the container that is mounting this volume for some reason. Instead, we have to figure out which container has this volume mounted via the API.
                    var client = new DockerClientConfiguration().CreateClient();
                    var containers = await client.Containers.ListContainersAsync(new global::Docker.DotNet.Models.ContainersListParameters
                    {
                        All = true,
                        Filters = new Dictionary<string, IDictionary<string, bool>>
                        {
                            {
                                "volume",
                                new Dictionary<string, bool>
                                {
                                    {  volume.Name, true }
                                }
                            }
                        }
                    }).ConfigureAwait(false);
                    if (containers.Count == 0)
                    {
                        throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                        {
                            Err = $"Internal error: This volume name is not associated with any containers.",
                            Mountpoint = string.Empty,
                        });
                    }
                    else if (containers.Count > 1)
                    {
                        throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                        {
                            Err = $"Internal error: This volume name is associated with too many containers.",
                            Mountpoint = string.Empty,
                        });
                    }

                    // Assign the container ID.
                    volume.ContainerID = containers[0].ID;
                    _logger.LogInformation($"Detected container ID as: {volume.ContainerID}");

                    // Locate the layerchain.json file in our windowsfilter directory.
                    // The windowsfilter directory is where Docker Engine has all of
                    // the container data and extracted images. We use layerchain.json
                    // to find the first normal file at the path the volume was 
                    // created for, and use that for our package reader.
                    var layerChainPath = Path.Combine(
                        Environment.GetEnvironmentVariable("PROGRAMDATA")!,
                        "docker",
                        "windowsfilter",
                        volume.ContainerID,
                        "layerchain.json");
                    var layerChain = JsonSerializer.Deserialize(
                        File.ReadAllText(layerChainPath),
                        DockerJsonSerializerContext.Default.StringArray);
                    string? targetPath = null;
                    foreach (var layer in layerChain!)
                    {
                        var relativePath = volume.FilePath["C:\\".Length..];
                        var descendantPath = Path.Combine(layer, "Files", relativePath);

                        _logger.LogTrace($"Checking for package file: {descendantPath}");

                        var fileInfo = new FileInfo(descendantPath);
                        if ((fileInfo.Attributes & FileAttributes.ReparsePoint) == 0)
                        {
                            targetPath = fileInfo.FullName;
                            break;
                        }
                    }
                    if (targetPath == null)
                    {
                        throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                        {
                            Err = $"No such path '{volume.FilePath}' exists inside the container, so the volume can not be mounted.",
                            Mountpoint = string.Empty,
                        });
                    }
                    else
                    {
                        _logger.LogInformation($"Located package within layerchain at: {targetPath}");
                    }

                    // Figure out the mounter to use.
                    IPackageMounter? selectedMounter = _packageMounterDetector.CreateMounterForPackage(targetPath);
                    if (selectedMounter == null)
                    {
                        throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                        {
                            Err = $"Unable to detect the type of package located at '{volume.FilePath}'. Make sure the file is a valid package.",
                            Mountpoint = string.Empty,
                        });
                    }

                    // Run basic checks on the mounter.
                    if (selectedMounter.RequiresAdminPermissions)
                    {
                        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
                        {
                            throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                            {
                                Err = $"To mount this package, the UEFS Docker plugin service must be running with administrative permissions.",
                                Mountpoint = string.Empty,
                            });
                        }
                    }
                    if (!selectedMounter.CompatibleWithDocker)
                    {
                        throw new EndpointException<DockerMountResponse>(400, new DockerMountResponse
                        {
                            Err = $"This type of package can not currently be mounted in Docker containers due to limitations on Windows.",
                            Mountpoint = string.Empty,
                        });
                    }


                    // Generate the mountpoint.
                    volume.Mountpoint = Path.Combine(
                        Environment.GetEnvironmentVariable("PROGRAMDATA")!,
                        "docker",
                        "plugins",
                        "uefs",
                        "mounts",
                        hash);
                    var writeStorage = Path.Combine(
                        Environment.GetEnvironmentVariable("PROGRAMDATA")!,
                        "docker",
                        "plugins",
                        "uefs",
                        "writelayers",
                        hash);
                    Directory.CreateDirectory(volume.Mountpoint);

                    var directory = new DirectoryInfo(volume.Mountpoint);
                    _logger.LogInformation($"Mounting at path on host: {volume.Mountpoint}");

                    try
                    {
                        volume.PackageMounter = selectedMounter;
                        await selectedMounter.MountAsync(targetPath, volume.Mountpoint, writeStorage, WriteScratchPersistence.DiscardOnUnmount).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        throw new EndpointException<DockerMountResponse>(500, new DockerMountResponse
                        {
                            Err = $"Failed to mount the package: {ex}",
                            Mountpoint = string.Empty,
                        });
                    }

                    return new DockerMountResponse
                    {
                        Err = string.Empty,
                        Mountpoint = volume.Mountpoint,
                    };
                }
                catch
                {
                    volume.ContainerID = null;
                    volume.Mountpoint = null;
                    if (volume.PackageMounter != null)
                    {
                        await volume.PackageMounter.DisposeAsync().ConfigureAwait(false);
                    }
                    volume.PackageMounter = null;
                    throw;
                }
            }
            finally
            {
                volume.Mutex.Release();
            }
        }
    }
}
