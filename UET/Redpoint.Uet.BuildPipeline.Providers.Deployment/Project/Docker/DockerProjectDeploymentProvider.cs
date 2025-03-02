namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Docker
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProcessExecution.Enumerable;
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
                                { "ProjectName", "$(ProjectName)" },
                                { "StageDirectory", "$(StageDirectory)" },
                                { "TempPath", "$(TempPath)" },
                                { "Timestamp", "$(Timestamp)" },
                                { "ReleaseVersion", "$(ReleaseVersion)" },
                                { "RepositoryRoot", "$(RepositoryRoot)" },
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

            var repositoryRoot = runtimeSettings["RepositoryRoot"];

            var packagePath = Path.Combine(runtimeSettings["StageDirectory"], $"{config.Package.Platform}Server");

            var filenamesToCheck = new[]
            {
                $"{config.Package.Target}-{config.Package.Platform}-{config.Package.Configuration}-Cmd",
                $"{config.Package.Target}-Cmd",
                $"{config.Package.Target}-{config.Package.Platform}-{config.Package.Configuration}",
                $"{config.Package.Target}",
            };
            var filename = filenamesToCheck.FirstOrDefault(x => File.Exists(Path.Combine(packagePath, runtimeSettings["ProjectName"], "Binaries", config.Package.Platform, x)));
            if (filename == null)
            {
                _logger.LogError("Unable to find binary in staged build.");
                return 1;
            }

            var symbolRemove = config.KeepSymbols
                ? string.Empty
                : $"RUN rm -f /srv/*/Binaries/{config.Package.Platform}/*.debug /srv/*/Binaries/{config.Package.Platform}/*.sym";

            var dockerfileContent =
                $"""
                FROM ubuntu AS builder
                COPY . /srv
                RUN chmod a+x /srv/*.sh /srv/*/Binaries/{config.Package.Platform}/*
                {symbolRemove}
                FROM gcr.io/distroless/cc-debian10:nonroot
                COPY --from=builder --chown=nonroot:nonroot /srv /home/nonroot/server
                ENTRYPOINT [ "/home/nonroot/server/{runtimeSettings["ProjectName"]}/Binaries/{config.Package.Platform}/{filename}", "{runtimeSettings["ProjectName"]}", "-stdout", "-FullStdOutLogOutput" ]
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
            var buildExitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = docker,
                    Arguments = arguments,
                    WorkingDirectory = packagePath,
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            if (buildExitCode != 0)
            {
                _logger.LogError("Docker build failed.");
                return buildExitCode;
            }

            if (config.Helm != null && config.Helm.Length > 0)
            {
                _logger.LogInformation($"There are {config.Helm.Length} Helm deployments to run.");

                string helm;
                try
                {
                    helm = await _pathResolver.ResolveBinaryPath("helm");
                }
                catch
                {
                    _logger.LogError("'helm' not found on PATH.");
                    return 1;
                }

                foreach (var helmDeploy in config.Helm)
                {
                    var kubeContext = helmDeploy.KubeContext;
                    var kubeContextOverride = Environment.GetEnvironmentVariable($"UET_KUBECONTEXT_OVERRIDE_{kubeContext}");
                    if (!string.IsNullOrWhiteSpace(kubeContextOverride))
                    {
                        kubeContext = kubeContextOverride;
                    }

                    var helmChartPath = helmDeploy.HelmChartPath;
                    if (!Path.IsPathRooted(helmChartPath))
                    {
                        helmChartPath = Path.GetFullPath(Path.Combine(repositoryRoot, helmChartPath));
                    }
                    _logger.LogInformation($"Deploying Helm chart '{helmDeploy.Name}' from path: {helmChartPath}");

                    var deployArgs = new List<LogicalProcessArgument>
                    {
                        "--kube-context",
                        kubeContext,
                        "upgrade",
                        "--install",
                        helmDeploy.Name,
                        helmChartPath,
                        "--namespace",
                        helmDeploy.Namespace,
                        "--create-namespace",
                        "--set",
                        $"agones.image={imageTag}",
                        "--set",
                        $"agones.version={runtimeSettings["ReleaseVersion"]}",
                    };
                    if (helmDeploy.HelmValues != null)
                    {
                        foreach (var kv in helmDeploy.HelmValues)
                        {
                            deployArgs.Add("--set");
                            deployArgs.Add($"{kv.Key}={kv.Value}");
                        }
                    }

                    var deployExitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = helm,
                            Arguments = deployArgs,
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);
                    if (deployExitCode != 0)
                    {
                        _logger.LogError("Helm deployment failed.");
                        return deployExitCode;
                    }
                }
            }

            _logger.LogInformation($"All post-Docker deployments completed.");
            return 0;
        }
    }
}
