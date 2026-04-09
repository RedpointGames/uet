namespace UET.Commands.Internal.BuildMultiPlatformContainer
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Text;
    using System.Text.Json;

    internal class BuildMultiPlatformContainerCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<BuildMultiPlatformContainerCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("build-multi-platform-container", "Builds a multi-platform Docker container and pushes it to the registry.");
                })
            .Build();

        internal sealed class Options
        {
            public Option<FileInfo> LinuxDockerfile = new("--linux-dockerfile");
            public Option<FileInfo> WindowsDockerfile = new("--windows-dockerfile");
        }

        private sealed class BuildMultiPlatformContainerCommandInstance : ICommandInstance
        {
            private readonly ILogger<BuildMultiPlatformContainerCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly IPathResolver _pathResolver;
            private readonly Options _options;

            public BuildMultiPlatformContainerCommandInstance(
                ILogger<BuildMultiPlatformContainerCommandInstance> logger,
                IProcessExecutor processExecutor,
                IPathResolver pathResolver,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _pathResolver = pathResolver;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var linuxDockerfile = context.ParseResult.GetValueForOption(_options.LinuxDockerfile);
                var windowsDockerfile = context.ParseResult.GetValueForOption(_options.WindowsDockerfile);

                var docker = await _pathResolver.ResolveBinaryPath("docker");

                var buildContext = new DirectoryInfo(Environment.GetEnvironmentVariable("BUILD_CONTEXT")!);
                var outputType = Environment.GetEnvironmentVariable("PUSH_IMAGE") == "true" ? "registry" : "image";

                var image = Environment.GetEnvironmentVariable("IMAGE")!;
                var imageWithoutTag = image.Split(':')[0];

                var builderId = "uet-docker-builder";

                _logger.LogInformation("Creating Docker build context...");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = docker,
                        Arguments = [
                            "buildx",
                            "create",
                            "--name",
                            builderId,
                            "--driver",
                            "docker-container",
                            "--driver-opt",
                            "image=moby/buildkit:v0.9.3",
                        ]
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());

                var tempPath = Path.GetTempFileName();

                var digests = new List<string>();

                int exitCode = 0;

                if (linuxDockerfile != null && linuxDockerfile.Exists)
                {
                    if (outputType == "image")
                    {
                        _logger.LogInformation("Building image for Linux...");
                    }
                    else
                    {
                        _logger.LogInformation("Building and pushing image for Linux...");
                    }

                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = docker,
                            Arguments = [
                                "buildx",
                                "build",
                                "--builder",
                                builderId,
                                "--platform",
                                "linux/amd64",
                                $"--output",
                                "push-by-digest=true,type=image,push=true",
                                $"--metadata-file",
                                tempPath,
                                "-f",
                                Path.GetRelativePath(buildContext.FullName, linuxDockerfile.FullName),
                                "-t",
                                imageWithoutTag,
                                Environment.GetEnvironmentVariable("BUILD_CONTEXT")!,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        return exitCode;
                    }

                    var newDigest = JsonSerializer.Deserialize(
                        File.ReadAllText(tempPath),
                        DockerBuildxManifestJsonSerializerContext.Default.DockerBuildxManifest)!.Digest;
                    _logger.LogInformation($"Linux image built as digest {newDigest}.");
                    digests.Add(newDigest);
                }

                if (windowsDockerfile != null && windowsDockerfile.Exists)
                {
                    if (outputType == "image")
                    {
                        _logger.LogInformation("Building image for Windows...");
                    }
                    else
                    {
                        _logger.LogInformation("Building and pushing image for Windows...");
                    }

                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = docker,
                            Arguments = [
                                "buildx",
                                "build",
                                "--builder",
                                builderId,
                                "--platform",
                                "windows/amd64",
                                $"--output",
                                "push-by-digest=true,type=image,push=true",
                                $"--metadata-file",
                                tempPath,
                                "-f",
                                Path.GetRelativePath(buildContext.FullName, windowsDockerfile.FullName),
                                "-t",
                                imageWithoutTag,
                                Environment.GetEnvironmentVariable("BUILD_CONTEXT")!,
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        return exitCode;
                    }

                    var newDigest = JsonSerializer.Deserialize(
                        File.ReadAllText(tempPath),
                        DockerBuildxManifestJsonSerializerContext.Default.DockerBuildxManifest)!.Digest;
                    _logger.LogInformation($"Windows image built as digest {newDigest}.");
                    digests.Add(newDigest);
                }

                var manifestArguments = new List<LogicalProcessArgument>
                {
                    "buildx",
                    "imagetools",
                    "--builder",
                    builderId,
                    "create",
                    "-t",
                    image,
                };
                foreach (var digest in digests)
                {
                    manifestArguments.Add($"{imageWithoutTag}@{digest}");
                }

                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = docker,
                        Arguments = manifestArguments
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    return exitCode;
                }
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = docker,
                        Arguments = [
                            "buildx",
                            "imagetools",
                            "--builder",
                            builderId,
                            "inspect",
                            image,
                        ]
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken());
                if (exitCode != 0)
                {
                    return exitCode;
                }

                _logger.LogInformation("All done!");
                return 0;
            }
        }
    }
}
