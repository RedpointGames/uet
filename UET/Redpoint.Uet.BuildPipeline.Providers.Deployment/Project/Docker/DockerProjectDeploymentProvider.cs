namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.RuntimeJson;
    using Redpoint.Uet.BuildGraph;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Package;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Project;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Xml;

    internal sealed class DockerProjectDeploymentProvider : IProjectDeploymentProvider, IDynamicReentrantExecutor<BuildConfigProjectDistribution, BuildConfigProjectDeploymentDocker>
    {
        private readonly ILogger<DockerProjectDeploymentProvider> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IPathResolver _pathResolver;
        private readonly IGlobalArgsProvider? _globalArgsProvider;

        public DockerProjectDeploymentProvider(
            ILogger<DockerProjectDeploymentProvider> logger,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress,
            IPathResolver pathResolver,
            IGlobalArgsProvider? globalArgsProvider = null)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
            _pathResolver = pathResolver;
            _globalArgsProvider = globalArgsProvider;
        }

        public string Type => "Docker";

        public IRuntimeJson DynamicSettings { get; } = new DeploymentProviderRuntimeJson(DeploymentProviderSourceGenerationContext.WithStringEnum).BuildConfigProjectDeploymentDocker;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigProjectDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, manual: x.Manual ?? false, settings: (BuildConfigProjectDeploymentDocker)x.DynamicSettings))
                .ToList();

            // Emit the nodes to run each deployment.
            foreach (var (name, manual, settings) in castedSettings)
            {
                var stageName = $"{settings.Package.Type}Staged_{settings.Package.Target}_{settings.Package.Platform}_{settings.Package.Configuration}";
                if (!string.IsNullOrWhiteSpace(settings.Package.CookFlavor))
                {
                    stageName = $"{stageName}_{settings.Package.CookFlavor}";
                }

                await writer.WriteAgentNodeAsync(
                    new AgentNodeElementProperties
                    {
                        AgentStage = $"Deployment {name}",
                        AgentType = manual ? "Win64_Manual" : "Win64",
                        NodeName = $"Deployment {name}",
                        Requires = $"#{stageName};$(DynamicPreDeploymentNodes)",
                    },
                    async writer =>
                    {
                        await writer.WriteDynamicReentrantSpawnAsync<DockerProjectDeploymentProvider, BuildConfigProjectDistribution, BuildConfigProjectDeploymentDocker>(
                            this,
                            context,
                            $"{settings.Package.Platform}.{name}".Replace(" ", ".", StringComparison.Ordinal),
                            settings,
                            new Dictionary<string, string>
                            {
                                { "ProjectRoot", "$(ProjectRoot)" },
                                { "StageDirectory", "$(StageDirectory)" },
                                { "TempPath", "$(TempPath)" },
                                { "Timestamp", "$(Timestamp)" },
                                { "ReleaseVersion", "$(ReleaseVersion)" },
                            }).ConfigureAwait(false);
                        await writer.WriteDynamicNodeAppendAsync(
                            new DynamicNodeAppendElementProperties
                            {
                                NodeName = $"Deployment {name}",
                            }).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigProjectDeploymentDocker)configUnknown;

            var imageTag = $"{config.Image}:{runtimeSettings["ReleaseVersion"]}";

            var packagePath = Path.Combine(runtimeSettings["StageDirectory"], "LinuxServer");
            var shellFile = Directory.GetFiles(packagePath, "*.sh").FirstOrDefault();
            if (shellFile == null)
            {
                _logger.LogError("Missing .sh file in staged dedicated server build.");
                return 1;
            }

            var dockerfileContent =
                $"""
                FROM ubuntu:20.04
                ADD . /app
                RUN chown -R 1000:1000 /app
                RUN chmod a+x /app/*.sh /app/*/Binaries/Linux/*
                USER 1000:1000
                ENV HOME=/app
                WORKDIR /app
                ENTRYPOINT [ "/app/{Path.GetFileName(shellFile)}" ]
                """;

            string docker;
            try
            {
                docker = await _pathResolver.ResolveBinaryPath("docker");
            }
            catch
            {
                _logger.LogError("'docker' not found on PATH.");
                return 1;
            }

            var arguments = new List<LogicalProcessArgument>
            {
                "buildx",
                "build",
                "-t",
                imageTag,
            };
            if (config.Push)
            {
                arguments.Add("--push");
            }
            arguments.Add(".");

            File.WriteAllText(Path.Combine(packagePath, "Dockerfile"), dockerfileContent);

            _logger.LogInformation($"Building Docker container under {packagePath}...");
            return await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = docker,
                    Arguments = arguments,
                    WorkingDirectory = packagePath,
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
        }
    }
}
