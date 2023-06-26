namespace Redpoint.UET.BuildPipeline.Executors.BuildServer
{
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph.Export;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Executors.Engine;
    using Redpoint.UET.Workspace;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using System.Linq;
    using System.Text.Json;
    using Redpoint.UET.Core;
    using System.Reflection;
    using System.Diagnostics;
    using Redpoint.UET.Workspace.Descriptors;
    using Redpoint.UET.Configuration;
    using System.Diagnostics.CodeAnalysis;

    public abstract class BuildServerBuildExecutor : IBuildExecutor
    {
        private readonly ILogger<BuildServerBuildExecutor> _logger;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IDynamicWorkspaceProvider _workspaceProvider;
        private readonly IGlobalArgsProvider? _globalArgsProvider;
        private readonly string _buildServerOutputFilePath;

        public BuildServerBuildExecutor(
            ILogger<BuildServerBuildExecutor> logger,
            IBuildGraphExecutor buildGraphExecutor,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IDynamicWorkspaceProvider workspaceProvider,
            IGlobalArgsProvider? globalArgsProvider,
            string buildServerOutputFilePath)
        {
            _logger = logger;
            _buildGraphExecutor = buildGraphExecutor;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _workspaceProvider = workspaceProvider;
            _globalArgsProvider = globalArgsProvider;
            _buildServerOutputFilePath = buildServerOutputFilePath;
        }

        private struct UETPreparationInfo
        {
            public string? WindowsPath { get; set; }
            public string? MacPath { get; set; }
            public string? LinuxPath { get; set; }
        }

        [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "This method is aware of single file publishing.")]
        private async Task<UETPreparationInfo> PrepareUETStorageAsync(
            string windowsSharedStoragePath,
            string? macSharedStoragePath,
            string? linuxSharedStoragePath,
            bool requiresCrossPlatformForBuild)
        {
            windowsSharedStoragePath = windowsSharedStoragePath.TrimEnd('\\');
            macSharedStoragePath = macSharedStoragePath?.TrimEnd('/');
            linuxSharedStoragePath = linuxSharedStoragePath?.TrimEnd('/');

            string localOsSharedStoragePath;
            if (OperatingSystem.IsWindows())
            {
                localOsSharedStoragePath = windowsSharedStoragePath;
            }
            else if (OperatingSystem.IsMacOS())
            {
                if (macSharedStoragePath == null)
                {
                    throw new InvalidOperationException();
                }
                localOsSharedStoragePath = macSharedStoragePath;
            }
#if ENABLE_LINUX_SUPPORT
            else if (OperatingSystem.IsLinux())
            {
                if (linuxSharedStoragePath == null)
                {
                    throw new InvalidOperationException();
                }
                localOsSharedStoragePath = linuxSharedStoragePath;
            }
#endif
            else
            {
                throw new PlatformNotSupportedException();
            }

            var targetFolder = Path.Combine(localOsSharedStoragePath, "uet");
            Directory.CreateDirectory(targetFolder);

            var preparationInfo = new UETPreparationInfo();

            if (requiresCrossPlatformForBuild)
            {
                // Check that we can run cross-platform builds.
                var entryAssembly = Assembly.GetEntryAssembly()!;
                var manifestNames = entryAssembly.GetManifestResourceNames();
                if (!string.IsNullOrWhiteSpace(entryAssembly.Location) ||
                    !manifestNames.Any(x => x.StartsWith("UET.Embedded.")))
                {
                    throw new BuildPipelineExecutionFailure("UET is not built as a self-contained cross-platform binary, and the build contains cross-platform targets. Create a version of UET with 'dotnet msbuild -restore -t:PublishAllRids' and use the resulting binary.");
                }

                // Copy the binaries for other platforms from our embedded resources.
                foreach (var manifestName in manifestNames)
                {
                    if (manifestName.StartsWith("UET.Embedded."))
                    {
                        using (var stream = entryAssembly.GetManifestResourceStream(manifestName)!)
                        {
                            string targetName;
#if ENABLE_LINUX_SUPPORT
                            if (manifestName.StartsWith("UET.Embedded.linux") && linuxSharedStoragePath != null)
                            {
                                targetName = "uet.linux";
                                preparationInfo.LinuxPath = $"{linuxSharedStoragePath}/uet/uet.linux";
                            }
                            else 
#endif
                            if (manifestName.StartsWith("UET.Embedded.osx") && macSharedStoragePath != null)
                            {
                                targetName = "uet.osx";
                                preparationInfo.MacPath = $"{macSharedStoragePath}/uet/uet.osx";
                            }
                            else if (manifestName.StartsWith("UET.Embedded.win"))
                            {
                                targetName = "uet.win.exe";
                                preparationInfo.WindowsPath = $"{windowsSharedStoragePath}\\uet\\uet.win.exe";
                            }
                            else
                            {
                                continue;
                            }
                            using (var target = new FileStream(Path.Combine(targetFolder, targetName), FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                await stream.CopyToAsync(target);
                            }
                        }
                    }
                }

                // Copy our own binary for the current platform.
                string selfTargetPath;
                if (OperatingSystem.IsWindows())
                {
                    selfTargetPath = $"{windowsSharedStoragePath}\\uet\\uet.win.exe";
                    preparationInfo.WindowsPath = selfTargetPath;
                }
                else if (OperatingSystem.IsMacOS())
                {
                    if (macSharedStoragePath == null)
                    {
                        throw new InvalidOperationException();
                    }
                    selfTargetPath = $"{macSharedStoragePath}/uet/uet.osx";
                    preparationInfo.MacPath = selfTargetPath;
                }
#if ENABLE_LINUX_SUPPORT
                else if (OperatingSystem.IsLinux())
                {
                    if (linuxSharedStoragePath == null)
                    {
                        throw new InvalidOperationException();
                    }
                    selfTargetPath = $"{linuxSharedStoragePath}/uet/uet.linux";
                    preparationInfo.LinuxPath = selfTargetPath;
                }
#endif
                else
                {
                    throw new PlatformNotSupportedException();
                }
                File.Copy(Process.GetCurrentProcess().MainModule!.FileName, selfTargetPath, true);
            }
            else
            {
                // Copy our UET executable (and it's dependencies if needed) to the shared storage folder.
                if (string.IsNullOrWhiteSpace(Assembly.GetEntryAssembly()?.Location))
                {
                    // This is a self-contained executable.
                    string selfTargetPath;
                    if (OperatingSystem.IsWindows())
                    {
                        selfTargetPath = $"{windowsSharedStoragePath}\\uet\\uet.win.exe";
                        preparationInfo.WindowsPath = selfTargetPath;
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        if (macSharedStoragePath == null)
                        {
                            throw new InvalidOperationException();
                        }
                        selfTargetPath = $"{macSharedStoragePath}/uet/uet.osx";
                        preparationInfo.MacPath = selfTargetPath;
                    }
#if ENABLE_LINUX_SUPPORT
                    else if (OperatingSystem.IsLinux())
                    {
                        if (linuxSharedStoragePath == null)
                        {
                            throw new InvalidOperationException();
                        }
                        selfTargetPath = $"{linuxSharedStoragePath}/uet/uet.linux";
                        preparationInfo.LinuxPath = selfTargetPath;
                    }
#endif
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }
                    File.Copy(Process.GetCurrentProcess().MainModule!.FileName, selfTargetPath, true);
                }
                else
                {
                    // This is a normal .NET app (during development).
                    _logger.LogInformation($"Recursively copying UET from {AppContext.BaseDirectory} to {Path.Combine(localOsSharedStoragePath, "uet")}...");
                    await DirectoryAsync.CopyAsync(
                        AppContext.BaseDirectory,
                        Path.Combine(localOsSharedStoragePath, "uet"),
                        true);
                    if (OperatingSystem.IsWindows())
                    {
                        preparationInfo.WindowsPath = $"{windowsSharedStoragePath}\\uet\\uet.exe";
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        if (macSharedStoragePath == null)
                        {
                            throw new InvalidOperationException();
                        }
                        preparationInfo.MacPath = $"{macSharedStoragePath}/uet/uet";
                    }
#if ENABLE_LINUX_SUPPORT
                    else if (OperatingSystem.IsLinux())
                    {
                        if (linuxSharedStoragePath == null)
                        {
                            throw new InvalidOperationException();
                        }
                        preparationInfo.LinuxPath = $"{linuxSharedStoragePath}/uet/uet";
                    }
#endif
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }
                }
            }

            if (preparationInfo.WindowsPath != null)
            {
                _logger.LogInformation($"UET (windows): Copied to {preparationInfo.WindowsPath}");
            }
            else
            {
                _logger.LogInformation($"UET (windows): Not copied");
            }
            if (preparationInfo.MacPath != null)
            {
                _logger.LogInformation($"UET (mac    ): Copied to {preparationInfo.MacPath}");
            }
            else
            {
                _logger.LogInformation($"UET (mac    ): Not copied");
            }
#if ENABLE_LINUX_SUPPORT
            if (preparationInfo.LinuxPath != null)
            {
                _logger.LogInformation($"UET (linux  ): Copied to {preparationInfo.LinuxPath}");
            }
            else
            {
                _logger.LogInformation($"UET (linux  ): Not copied");
            }
#endif

            return preparationInfo;
        }

        public virtual async Task<int> ExecuteBuildAsync(
            BuildSpecification buildSpecification,
            IBuildExecutionEvents buildExecutionEvents,
            ICaptureSpecification generationCaptureSpecification,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_buildServerOutputFilePath))
            {
                throw new BuildPipelineExecutionFailure("This build executor requires BuildServerOutputFilePath to be set.");
            }

            BuildGraphExport buildGraph;
            await using (var engineWorkspace = await _engineWorkspaceProvider.GetEngineWorkspace(
                buildSpecification.Engine,
                string.Empty,
                cancellationToken))
            {
                // @note: Generating the BuildGraph doesn't require any files from the workspace, so we don't bother
                // setting up a Git workspace for it.
                await using (var temporaryWorkspace = await _workspaceProvider.GetWorkspaceAsync(
                    new TemporaryWorkspaceDescriptor { Name = "Generate BuildGraph JSON" },
                    cancellationToken))
                {
                    _logger.LogInformation("Generating BuildGraph JSON based on settings...");
                    buildGraph = await _buildGraphExecutor.GenerateGraphAsync(
                        engineWorkspace.Path,
                        temporaryWorkspace.Path,
                        buildSpecification.UETPath,
                        buildSpecification.BuildGraphScript,
                        buildSpecification.BuildGraphTarget,
                        OperatingSystem.IsWindows()
                            ? buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath
                            : buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                        buildSpecification.BuildGraphSettings,
                        buildSpecification.BuildGraphSettingReplacements,
                        generationCaptureSpecification,
                        cancellationToken);
                }
            }

            var agentTypeMapping = new Dictionary<string, BuildServerJobPlatform>
            {
                { "Win64", BuildServerJobPlatform.Windows },
                { "Win64_Licensee", BuildServerJobPlatform.Windows },
                { "HoloLens", BuildServerJobPlatform.Windows },
                { "Mac", BuildServerJobPlatform.Mac },
                { "Mac_Licensee", BuildServerJobPlatform.Mac },
                { "Meta", BuildServerJobPlatform.Meta },
            };

            var nodeMap = GetNodeMap(buildGraph);

            var pipeline = new BuildServerPipeline();
            pipeline.GlobalEnvironmentVariables.Add(
                "UET_USE_STORAGE_VIRTUALIZATION",
                buildSpecification.BuildGraphEnvironment.UseStorageVirtualisation ? "true" : "false");

            if (buildSpecification.GlobalEnvironmentVariables != null)
            {
                foreach (var kv in buildSpecification.GlobalEnvironmentVariables)
                {
                    buildSpecification.GlobalEnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            var requiresCrossPlatformBuild = false;
            foreach (var group in buildGraph.Groups)
            {
                if (group.AgentTypes.Length == 0 ||
                    !agentTypeMapping.ContainsKey(group.AgentTypes[0]))
                {
                    throw new NotSupportedException($"Unknown AgentType specified in BuildGraph: {string.Join(",", group.AgentTypes)}");
                }

                var targetPlatform = agentTypeMapping[group.AgentTypes[0]];
                if (targetPlatform == BuildServerJobPlatform.Mac && !OperatingSystem.IsMacOS())
                {
                    requiresCrossPlatformBuild = true;
                }
                else if (targetPlatform == BuildServerJobPlatform.Windows && !OperatingSystem.IsWindows())
                {
                    requiresCrossPlatformBuild = true;
                }
            }

            var preparationInfo = await PrepareUETStorageAsync(
                buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath,
                buildSpecification.BuildGraphEnvironment.Mac?.SharedStorageAbsolutePath,
                null,
                requiresCrossPlatformBuild);

            foreach (var group in buildGraph.Groups)
            {
                if (group.AgentTypes.Length == 0 ||
                    !agentTypeMapping.ContainsKey(group.AgentTypes[0]))
                {
                    throw new NotSupportedException($"Unknown AgentType specified in BuildGraph: {string.Join(",", group.AgentTypes)}");
                }

                foreach (var node in group.Nodes)
                {
                    var needs = new HashSet<string>();
                    GetFullDependenciesOfNode(nodeMap, node, needs);

                    if (node.Name == "End")
                    {
                        // This is a special job that we don't actually emit
                        // because it doesn't do anything.
                        continue;
                    }

                    var job = new BuildServerJob
                    {
                        Name = node.Name,
                        Stage = group.Name,
                        Needs = needs.ToArray(),
                        Platform = agentTypeMapping[group.AgentTypes[0]],
                        IsManual = node.Name.StartsWith("Deploy Manual "),
                    };

                    if (node.Name.StartsWith("Automation "))
                    {
                        job.ArtifactPaths = new[]
                        {
                            ".uet/tmp/*/TestResults_*.xml",
                            ".uet/tmp/T*/Saved/Logs/Worker*.log",
                        };
                        job.ArtifactJUnitReportPath = ".uet/tmp/*/TestResults_*.xml";
                    }

                    var globalArgs = _globalArgsProvider != null ? $" {_globalArgsProvider.GlobalArgsString}" : string.Empty;

                    switch (job.Platform)
                    {
                        case BuildServerJobPlatform.Windows:
                            {
                                job.Script = $"& \"{preparationInfo.WindowsPath}\"{globalArgs} internal ci-build --executor gitlab";
                                var buildJobJson = new BuildJobJson
                                {
                                    Engine = buildSpecification.Engine.ToReparsableString(),
                                    SharedStoragePath = buildSpecification.BuildGraphEnvironment.Windows.SharedStorageAbsolutePath,
                                    SdksPath = buildSpecification.BuildGraphEnvironment.Windows.SdksPath,
                                    BuildGraphTarget = buildSpecification.BuildGraphTarget,
                                    NodeName = node.Name,
                                    DistributionName = buildSpecification.DistributionName,
                                    BuildGraphScriptName = buildSpecification.BuildGraphScript.ToReparsableString(),
                                    PreparationScripts = buildSpecification.BuildGraphPreparationScripts.ToArray(),
                                    GlobalEnvironmentVariables = buildSpecification.GlobalEnvironmentVariables ?? new Dictionary<string, string>(),
                                    Settings = buildSpecification.BuildGraphSettings,
                                    ProjectFolderName = buildSpecification.ProjectFolderName,
                                };
                                job.EnvironmentVariables = new Dictionary<string, string>
                                {
                                    { "UET_BUILD_JSON", JsonSerializer.Serialize(buildJobJson, BuildJobJsonSourceGenerationContext.Default.BuildJobJson) },
                                };
                            }
                            break;
                        case BuildServerJobPlatform.Mac:
                            {
                                job.Script = $"chmod a+x \"{preparationInfo.MacPath}\" && \"{preparationInfo.MacPath}\"{globalArgs} internal ci-build --executor gitlab";
                                var buildJobJson = new BuildJobJson
                                {
                                    Engine = buildSpecification.Engine.ToReparsableString(),
                                    SharedStoragePath = buildSpecification.BuildGraphEnvironment.Mac!.SharedStorageAbsolutePath,
                                    SdksPath = buildSpecification.BuildGraphEnvironment.Mac.SdksPath,
                                    BuildGraphTarget = buildSpecification.BuildGraphTarget,
                                    NodeName = node.Name,
                                    DistributionName = buildSpecification.DistributionName,
                                    BuildGraphScriptName = buildSpecification.BuildGraphScript.ToReparsableString(),
                                    PreparationScripts = buildSpecification.BuildGraphPreparationScripts.ToArray(),
                                    GlobalEnvironmentVariables = buildSpecification.GlobalEnvironmentVariables ?? new Dictionary<string, string>(),
                                    Settings = buildSpecification.BuildGraphSettings,
                                    ProjectFolderName = buildSpecification.ProjectFolderName,
                                };
                                job.EnvironmentVariables = new Dictionary<string, string>
                                {
                                    { "UET_BUILD_JSON", JsonSerializer.Serialize(buildJobJson, BuildJobJsonSourceGenerationContext.Default.BuildJobJson) },
                                };
                            }
                            break;
                        case BuildServerJobPlatform.Meta:
                            break;
                        default:
                            throw new NotImplementedException();
                    }

                    pipeline.Stages.Add(job.Stage);
                    pipeline.Jobs.Add(job.Name, job);
                }
            }

            await EmitBuildServerSpecificFileAsync(
                buildSpecification,
                pipeline,
                _buildServerOutputFilePath);
            return 0;
        }

        private Dictionary<string, BuildGraphExportNode> GetNodeMap(BuildGraphExport buildGraph)
        {
            return buildGraph.Groups.SelectMany(x => x.Nodes)
                .ToDictionary(k => k.Name, v => v);
        }

        private void GetFullDependenciesOfNode(
            Dictionary<string, BuildGraphExportNode> nodeMap,
            BuildGraphExportNode node,
            HashSet<string> allDependencies)
        {
            foreach (var dependency in node.DependsOn.Split(';'))
            {
                if (nodeMap.ContainsKey(dependency))
                {
                    allDependencies.Add(dependency);
                    GetFullDependenciesOfNode(nodeMap, nodeMap[dependency], allDependencies);
                }
            }
        }

        protected abstract Task EmitBuildServerSpecificFileAsync(
            BuildSpecification buildSpecification,
            BuildServerPipeline buildServerPipeline,
            string buildServerOutputFilePath);

        public abstract string DiscoverPipelineId();
    }
}