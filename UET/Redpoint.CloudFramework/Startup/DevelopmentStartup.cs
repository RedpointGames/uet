namespace Redpoint.CloudFramework.Startup
{
    using Docker.DotNet;
    using Docker.DotNet.Models;
    using ICSharpCode.SharpZipLib.Tar;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DevelopmentStartup : IHostedService
    {
        private readonly IHostEnvironment _hostEnvironment;
        private readonly ILogger<DevelopmentStartup> _logger;
        private readonly GoogleCloudUsageFlag _googleCloudUsage;
        private readonly IConfiguration _configuration;
        private readonly Func<IConfiguration, string, DevelopmentDockerContainer[]>? _dockerFactory;
        internal bool _didStart;

        internal static readonly string[] _pubsubArgs = new[]
        {
            "gcloud",
            "beta",
            "emulators",
            "pubsub",
            "start",
            "--host-port=0.0.0.0:9000"
        };
        internal static readonly string[] _datastoreArgs = new[]
        {
            "gcloud",
            "beta",
            "emulators",
            "datastore",
            "start",
            // Firestore guarantees strong consistency now, so this
            // should be reasonably safe.
            "--consistency=1.0",
            "--host-port=0.0.0.0:9001",
            "--no-store-on-disk"
        };

        public DevelopmentStartup(
            IHostEnvironment hostEnvironment,
            ILogger<DevelopmentStartup> logger,
            GoogleCloudUsageFlag googleCloudUsage,
            IConfiguration configuration,
            Func<IConfiguration, string, DevelopmentDockerContainer[]>? dockerFactory)
        {
            _hostEnvironment = hostEnvironment;
            _logger = logger;
            _googleCloudUsage = googleCloudUsage;
            _configuration = configuration;
            _dockerFactory = dockerFactory;
            _didStart = false;
        }

        private record ExpectedContainer
        {
            public required string Name { get; set; }
            public string? Image { get; set; } = null;
            public IReadOnlyList<string> Arguments { get; set; } = Array.Empty<string>();
            public IReadOnlyCollection<DeveloperDockerPort> Ports { get; set; } = Array.Empty<DeveloperDockerPort>();
            public IReadOnlyCollection<string> Env { get; set; } = Array.Empty<string>();
            public bool DoNotPull { get; internal set; } = false;
        }

        private class ConsoleLogProgress : IProgress<JSONMessage>
        {
            public void Report(JSONMessage value)
            {
            }
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                if (!_hostEnvironment.IsDevelopment())
                {
                    return;
                }

                if (Environment.GetEnvironmentVariable("NO_AUTOSTART_DEPENDENCIES") == "true")
                {
                    return;
                }

                var client = new DockerClientConfiguration().CreateClient();

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _logger.LogInformation("Ensuring that Docker Desktop is running...");
                    bool connected = false;
                    bool startedDocker = false;
                    while (!connected)
                    {
                        try
                        {
                            await client.Containers.ListContainersAsync(new ContainersListParameters { Limit = 1 }, cancellationToken).ConfigureAwait(false);
                            connected = true;
                        }
                        catch (Exception ex) when (ex is TimeoutException || ex is DockerApiException)
                        {
                            if (!startedDocker)
                            {
                                if (Process.GetProcessesByName("Docker Desktop.exe").Length == 0)
                                {
                                    // Run gpupdate /force first.
                                    _logger.LogInformation("Running gpupdate /force before starting Docker Desktop...");
                                    var gpupdate = Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "gpupdate.exe"), "/force");
                                    gpupdate.WaitForExit();

                                    // Start Docker Desktop automatically.
                                    _logger.LogInformation("Starting Docker Desktop for you...");
                                    Process.Start(Path.Combine(
                                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                        "Docker",
                                        "Docker",
                                        "Docker Desktop.exe"));
                                    startedDocker = true;
                                }
                            }
                        }
                    }
                }

                var expectedContainers = new List<ExpectedContainer>();

                var developerContainers = _dockerFactory != null ? _dockerFactory(_configuration, _hostEnvironment.ContentRootPath) : Array.Empty<DevelopmentDockerContainer>();
                foreach (var developerContainer in developerContainers)
                {
                    // For developer containers, we have to make sure they're
                    // up to date and built first.
                    if (developerContainer.Image == null)
                    {
                        if (string.IsNullOrWhiteSpace(developerContainer.Context) ||
                            string.IsNullOrWhiteSpace(developerContainer.Dockerfile))
                        {
                            throw new InvalidOperationException($"You must set either 'Image' or ('Context' and 'Dockerfile') for each additional container configuration (for '{developerContainer.Name}' container).");
                        }

                        string? lastEntry = null;
                        await client.Images.BuildImageFromDockerfileAsync(new ImageBuildParameters
                            {
                                Dockerfile = developerContainer.Dockerfile,
                            }, CreateTarballForDockerfileDirectory(developerContainer.Context), Array.Empty<AuthConfig>(), new Dictionary<string, string>(), new ForwardingProgress<JSONMessage>((JSONMessage msg) =>
                            {
                                var entry = msg.Status ?? msg.Stream?.Trim();
                                if (!string.IsNullOrWhiteSpace(entry))
                                {
                                    if (lastEntry != entry)
                                    {
                                        _logger.LogInformation($"Building {developerContainer.Name}: {entry}");
                                    }
                                    lastEntry = entry;
                                }

                                if (entry != null && entry.StartsWith("Successfully built ", StringComparison.InvariantCulture))
                                {
                                    developerContainer.ImageId = entry.Substring("Successfully built ".Length);
                                }
                            }), cancellationToken).ConfigureAwait(false);
                        if (string.IsNullOrWhiteSpace(developerContainer.ImageId))
                        {
                            throw new DevelopmentStartupException($"Docker image for {developerContainer.Name} failed to build. Check the output for more information.");
                        }
                        expectedContainers.Add(
                            new ExpectedContainer
                            {
                                Name = developerContainer.Name,
                                Image = developerContainer.ImageId,
                                Arguments = developerContainer.Arguments ?? Array.Empty<string>(),
                                Ports = developerContainer.Ports,
                                Env = developerContainer.Environment.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
                                DoNotPull = true,
                            });
                    }
                    else
                    {
                        expectedContainers.Add(
                            new ExpectedContainer
                            {
                                Name = developerContainer.Name,
                                Image = developerContainer.Image,
                                Arguments = developerContainer.Arguments ?? Array.Empty<string>(),
                                Ports = developerContainer.Ports,
                                Env = developerContainer.Environment.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
                            });
                    }
                }

                expectedContainers.Add(
                    new ExpectedContainer
                    {
                        Name = "redis",
                        Image = "redis:6.0.10",
                        Arguments = { },
                        Ports = new DeveloperDockerPort[]
                        {
                            6379
                        }
                    });
                if ((_googleCloudUsage & GoogleCloudUsageFlag.PubSub) != 0)
                {
                    expectedContainers.Add(
                        new ExpectedContainer
                        {
                            Name = "pubsub",
                            Image = "gcr.io/google.com/cloudsdktool/cloud-sdk:latest",
                            Arguments = _pubsubArgs,
                            Ports = new DeveloperDockerPort[]
                            {
                                9000
                            }
                        }
                    );
                }
                if ((_googleCloudUsage & GoogleCloudUsageFlag.Datastore) != 0)
                {
                    expectedContainers.Add(
                        new ExpectedContainer
                        {
                            Name = "datastore",
                            Image = "gcr.io/google.com/cloudsdktool/cloud-sdk:latest",
                            Arguments = _datastoreArgs,
                            Ports = new DeveloperDockerPort[]
                            {
                                9001
                            }
                        }
                    );
                };

                var runningContainers = (await client.Containers.ListContainersAsync(new ContainersListParameters
                {
                    All = true,
                }, cancellationToken).ConfigureAwait(false));
                var runningContainersByName = new Dictionary<string, ContainerListResponse>();
                foreach (var runningContainer in runningContainers)
                {
                    foreach (var name in runningContainer.Names)
                    {
                        runningContainersByName[name] = runningContainer;
                    }
                }

                if ((_googleCloudUsage & GoogleCloudUsageFlag.PubSub) != 0 ||
                    (_googleCloudUsage & GoogleCloudUsageFlag.Datastore) != 0)
                {
                    _logger.LogInformation("This application will connect to the local Redis emulator.");
                }
                else
                {
                    _logger.LogInformation("This application will connect to the local Redis and Google Cloud emulators.");
                }

                // Create the network.
                var network = (await client.Networks.ListNetworksAsync(cancellationToken: cancellationToken).ConfigureAwait(false)).FirstOrDefault(x => x.Name == "cloud-framework")?.ID;
                if (network == null)
                {
                    network = (await client.Networks.CreateNetworkAsync(new NetworksCreateParameters
                    {
                        Name = "cloud-framework",
                    }, cancellationToken).ConfigureAwait(false)).ID;
                }

                foreach (var expectedContainer in expectedContainers)
                {
                    bool start = false;
                    if (runningContainersByName.ContainsKey("/" + expectedContainer.Name))
                    {
                        // This container is running, check to make sure it's arguments are correct.
                        var runningContainer = runningContainersByName["/" + expectedContainer.Name];
                        if (runningContainer.Image != expectedContainer.Image ||
                            runningContainer.State != "running" ||
                            !runningContainer.NetworkSettings.Networks.Any(x => x.Value.NetworkID == network) ||
                            !runningContainer.Ports.Select(x => x.PublicPort).All(x => expectedContainer.Ports.Contains(x)) ||
                            !expectedContainer.Ports.Select(x => x).All(x => runningContainer.Ports.Any(y => x.ContainerPort == y.PublicPort)))
                        {
                            // This container is wrong.
                            _logger.LogInformation($"Stopping and removing {expectedContainer.Name} container because it's configuration is incorrect.");
                            try
                            {
                                await client.Containers.KillContainerAsync(runningContainer.ID, new ContainerKillParameters
                                {
                                    Signal = "SIGKILL"
                                }, cancellationToken).ConfigureAwait(false);
                            }
                            catch { }
                            await client.Containers.RemoveContainerAsync(runningContainer.ID, new ContainerRemoveParameters
                            {
                                Force = true
                            }, cancellationToken).ConfigureAwait(false);
                            start = true;
                        }
                    }
                    else
                    {
                        // Container not running, always start it.
                        start = true;
                    }

                    if (!start)
                    {
                        continue;
                    }

                    if (!expectedContainer.DoNotPull)
                    {
                        if (!(await client.Images.ListImagesAsync(new ImagesListParameters
                        {
                            All = true,
                        }, cancellationToken).ConfigureAwait(false)).Any(x => x.RepoTags?.Contains(expectedContainer.Image) ?? false))
                        {
                            _logger.LogInformation($"Pulling the {expectedContainer.Image} image... (this might take a while)");
                            await client.Images.CreateImageAsync(new ImagesCreateParameters
                            {
                                FromImage = expectedContainer.Image,
                            }, null, new ConsoleLogProgress(), cancellationToken).ConfigureAwait(false);
                        }
                    }

                    _logger.LogInformation($"Launching {expectedContainer.Name} container because it is necessary for the development environment.");
                    var createdContainerConfig = new CreateContainerParameters
                    {
                        Name = expectedContainer.Name,
                        Image = expectedContainer.Image,
                        Cmd = expectedContainer.Arguments.ToList(),
                        ExposedPorts = expectedContainer.Ports.ToDictionary(k => k.ContainerPort.ToString(CultureInfo.InvariantCulture) + "/tcp", v => new EmptyStruct()),
                        HostConfig = new HostConfig
                        {
                            PortBindings = expectedContainer.Ports.ToDictionary(k => k.ContainerPort.ToString(CultureInfo.InvariantCulture) + "/tcp", v => (IList<PortBinding>)new List<PortBinding>
                        {
                            new PortBinding
                            {
                                // Only expose as "localhost" on the host machine.
                                HostIP = "127.0.0.1",
                                HostPort = v.HostPort.ToString(CultureInfo.InvariantCulture),
                            }
                        }),
                        },
                        NetworkingConfig = new NetworkingConfig
                        {
                            EndpointsConfig = new Dictionary<string, EndpointSettings>
                        {
                            {
                                network,
                                new EndpointSettings
                                {
                                    NetworkID = network,
                                    Aliases = new List<string>
                                    {
                                        expectedContainer.Name,
                                    }
                                }
                            }
                        }
                        },
                        Env = new List<string>
                    {
                        "CLOUDSDK_CORE_PROJECT=local-dev"
                    }.Concat(expectedContainer.Env ?? new List<string>()).ToList(),
                    };
                    var createdContainer = await client.Containers.CreateContainerAsync(createdContainerConfig, cancellationToken).ConfigureAwait(false);
                    await client.Containers.StartContainerAsync(createdContainer.ID, new ContainerStartParameters
                    {
                    }, cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                _didStart = true;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _didStart = false;
            return Task.CompletedTask;
        }

        private static MemoryStream CreateTarballForDockerfileDirectory(string directory)
        {
            var tarball = new MemoryStream();
            var files = Directory.GetFiles(directory, "*.*", SearchOption.AllDirectories);

            using var archive = new TarOutputStream(tarball, Encoding.UTF8)
            {
                //Prevent the TarOutputStream from closing the underlying memory stream when done
                IsStreamOwner = false
            };

            foreach (var file in files)
            {
                //Replacing slashes as KyleGobel suggested and removing leading /
                string tarName = file.Substring(directory.Length).Replace('\\', '/').TrimStart('/');

                //Let's create the entry header
                var entry = TarEntry.CreateTarEntry(tarName);
                using var fileStream = File.OpenRead(file);
                entry.Size = fileStream.Length;
                entry.TarHeader.Mode = Convert.ToInt32("100755", 8); //chmod 755
                archive.PutNextEntry(entry);

                //Now write the bytes of data
                byte[] localBuffer = new byte[32 * 1024];
                while (true)
                {
                    int numRead = fileStream.Read(localBuffer, 0, localBuffer.Length);
                    if (numRead <= 0)
                        break;

                    archive.Write(localBuffer, 0, numRead);
                }

                //Nothing more to do with this entry
                archive.CloseEntry();
            }
            archive.Close();

            //Reset the stream and return it, so it can be used by the caller
            tarball.Position = 0;
            return tarball;
        }

        private class ForwardingProgress<T> : IProgress<T>
        {
            private readonly Action<T> _func;

            public ForwardingProgress(Action<T> func)
            {
                _func = func;
            }

            public void Report(T value)
            {
                _func(value);
            }
        }
    }
}
