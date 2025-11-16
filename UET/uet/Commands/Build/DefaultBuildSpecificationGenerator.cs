namespace UET.Commands.Build
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.BuildGraph;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic;
    using Redpoint.Uet.BuildPipeline.Environment;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Core.BugReport;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.Uat;
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using UET.Commands.EngineSpec;
    using UET.Services;

    internal sealed class DefaultBuildSpecificationGenerator : IBuildSpecificationGenerator
    {
        private readonly ILogger<DefaultBuildSpecificationGenerator> _logger;
        private readonly ISelfLocation _selfLocation;
        private readonly IReleaseVersioning _versioning;
        private readonly IDynamicBuildGraphIncludeWriter _dynamicBuildGraphIncludeWriter;
        private readonly IWorldPermissionApplier _worldPermissionApplier;
        private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
        private readonly IBuildGraphExecutor _buildGraphExecutor;
        private readonly IGlobalArgsProvider? _globalArgsProvider;
        private readonly BugReportCollector? _bugReportCollector;

        public DefaultBuildSpecificationGenerator(
            ILogger<DefaultBuildSpecificationGenerator> logger,
            ISelfLocation selfLocation,
            IReleaseVersioning versioning,
            IDynamicBuildGraphIncludeWriter dynamicBuildGraphIncludeWriter,
            IWorldPermissionApplier worldPermissionApplier,
            IEngineWorkspaceProvider engineWorkspaceProvider,
            IBuildGraphExecutor buildGraphExecutor,
            IGlobalArgsProvider? globalArgsProvider = null,
            BugReportCollector? bugReportCollector = null)
        {
            _logger = logger;
            _selfLocation = selfLocation;
            _versioning = versioning;
            _dynamicBuildGraphIncludeWriter = dynamicBuildGraphIncludeWriter;
            _worldPermissionApplier = worldPermissionApplier;
            _engineWorkspaceProvider = engineWorkspaceProvider;
            _buildGraphExecutor = buildGraphExecutor;
            _globalArgsProvider = globalArgsProvider;
            _bugReportCollector = bugReportCollector;
        }

        private struct TargetConfig
        {
            public required string Targets;
            public required string TargetPlatforms;
            public required string Configurations;
            public required string CookFlavors;
        }

        private bool VerifySourceFilesDoNotExceedSubmissionPathLimit(
            string pluginName,
            DirectoryInfo sourceDirectory)
        {
            var hasErrored = false;

            void RecurseDirectory(DirectoryInfo rootDirectory, DirectoryInfo scanDirectory)
            {
                foreach (var subdirectory in scanDirectory.GetDirectories())
                {
                    RecurseDirectory(rootDirectory, subdirectory);
                }
                foreach (var file in scanDirectory.GetFiles())
                {
                    var fileFullLocalPath = file.FullName;
                    var rootDirectoryLocalPath = rootDirectory.FullName;
                    var fileRelativePath = fileFullLocalPath.Substring(rootDirectoryLocalPath.Length).TrimStart(Path.DirectorySeparatorChar);

                    var pathLengthOnSubmissionBuild = Path.Combine(
                        pluginName,
                        "Source",
                        fileRelativePath);

                    // 170 characters is the new limit imposed by Fab.
                    if (pathLengthOnSubmissionBuild.Length > 170)
                    {
                        if (!hasErrored)
                        {
                            _logger.LogError("The following paths are too long for this plugin to be submitted to the Marketplace/Fab. Please reduce the number of characters between the 'Source' directory and the end of the filename:");
                            hasErrored = true;
                        }
                        _logger.LogError($"  {pathLengthOnSubmissionBuild} ({pathLengthOnSubmissionBuild.Length} chars)");
                    }
                }
            }
            RecurseDirectory(sourceDirectory, sourceDirectory);

            return !hasErrored;
        }

        private TargetConfig ComputeTargetConfig(string name, BuildConfigProjectBuildTarget? target, bool localExecutor)
        {
            if (target == null)
            {
                return new TargetConfig
                {
                    Targets = string.Empty,
                    TargetPlatforms = string.Empty,
                    Configurations = string.Empty,
                    CookFlavors = string.Empty,
                };
            }

            var targets = target.Targets ?? new[] { $"Unreal{name}" };
            var targetPlatforms = FilterIncompatiblePlatforms(target.Platforms.Select(x => x.Platform).ToArray(), localExecutor);
            var configurations = target.Configurations ?? new[] { "Development", "Shipping" };
            var cookFlavors = target.Platforms.FirstOrDefault(x => x.Platform == "Android")?.CookFlavors ?? [];

            return new TargetConfig
            {
                Targets = string.Join(";", targets),
                TargetPlatforms = string.Join(";", targetPlatforms),
                Configurations = string.Join(";", configurations),
                CookFlavors = string.Join(";", cookFlavors),
            };
        }

        private TargetConfig ComputeTargetConfig(string name, BuildConfigPluginBuildTarget? target, bool localExecutor)
        {
            if (target == null)
            {
                return new TargetConfig
                {
                    Targets = string.Empty,
                    TargetPlatforms = string.Empty,
                    Configurations = string.Empty,
                    CookFlavors = string.Empty,
                };
            }

            var targets = new[] { $"Unreal{name}" };
            var targetPlatforms = FilterIncompatiblePlatforms(target.Platforms.Select(x => x.Platform).ToArray(), localExecutor);
            var configurations = target.Configurations ?? new[] { "Development", "Shipping" };

            return new TargetConfig
            {
                Targets = string.Join(";", targets),
                TargetPlatforms = string.Join(";", targetPlatforms),
                Configurations = string.Join(";", configurations),
                CookFlavors = string.Empty,
            };
        }

        private static string GetFilterInclude(
            string repositoryRoot,
            BuildConfigPluginDistribution distribution)
        {
            if (distribution.Package?.Filter == null)
            {
                return string.Empty;
            }
            var filterRules = new List<string>();
            var rawFilterRules = File.ReadAllLines(Path.Combine(repositoryRoot, distribution.Package.Filter));
            foreach (var rawFilterRule in rawFilterRules)
            {
                if (rawFilterRule == "[FilterPlugin]" ||
                    rawFilterRule.StartsWith(';') ||
                    rawFilterRule.StartsWith('-') ||
                    rawFilterRule.Trim().Length == 0)
                {
                    continue;
                }
                filterRules.Add(rawFilterRule);
            }
            return string.Join(";", filterRules);
        }

        private static string GetFilterExclude(
            string repositoryRoot,
            BuildConfigPluginDistribution distribution)
        {
            if (distribution.Package?.Filter == null)
            {
                return string.Empty;
            }
            var filterRules = new List<string>();
            var rawFilterRules = File.ReadAllLines(Path.Combine(repositoryRoot, distribution.Package.Filter));
            foreach (var rawFilterRule in rawFilterRules)
            {
                if (rawFilterRule == "[FilterPlugin]" ||
                    rawFilterRule.StartsWith(';') ||
                    !rawFilterRule.StartsWith('-') ||
                    rawFilterRule.Trim().Length == 0)
                {
                    continue;
                }
                filterRules.Add(rawFilterRule[1..]);
            }
            return string.Join(";", filterRules);
        }

        public async Task<BuildSpecification> BuildConfigEngineToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigEngineDistribution distribution,
            CancellationToken cancellationToken)
        {
            var availablePlatforms = new HashSet<string>();
            var availablePlatformsMac = new HashSet<string>();
            await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                engineSpec,
                "EngineBuildOptionAnalysis",
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var engine).ConfigureAwait(false))
            {
                var installedEngineBuildPath = Path.Combine(
                    engine.Path,
                    "Engine",
                    "Build",
                    "InstalledEngineBuild.xml");
                var installedEngineBuild = await File.ReadAllTextAsync(installedEngineBuildPath, cancellationToken).ConfigureAwait(false);
                await _buildGraphExecutor.ListGraphAsync(
                    engine.Path,
                    BuildGraphScriptSpecification.ForEngine(),
                    CaptureSpecification.CreateFromDelegates(new CaptureSpecificationDelegates
                    {
                        ReceiveStdout = (line) =>
                        {
                            line = line.Trim();
                            if (line.StartsWith("-set:With", StringComparison.Ordinal) &&
                                !line.StartsWith("-set:WithDDC", StringComparison.Ordinal) &&
                                !line.StartsWith("-set:WithClient", StringComparison.Ordinal) &&
                                !line.StartsWith("-set:WithServer", StringComparison.Ordinal) &&
                                !line.StartsWith("-set:WithFullDebugInfo", StringComparison.Ordinal))
                            {
                                line = line["-set:With".Length..];
                                line = line.Split('=')[0];
                                availablePlatforms.Add(line);
                                if (installedEngineBuild.Contains($@"<Option Name=""With{line}""", StringComparison.Ordinal))
                                {
                                    // macOS only knows about public (non-console) platforms.
                                    availablePlatformsMac.Add(line);
                                }
                            }
                            return false;
                        }
                    }),
                    cancellationToken).ConfigureAwait(false);
            }

            var settings = new Dictionary<string, string>
            {
                // Target types
                { "WithClient", distribution.Build.TargetTypes.Contains("Client") ? "true" : "false" },
                { "WithServer", distribution.Build.TargetTypes.Contains("Server") ? "true" : "false" },

                // Cook options
                { "WithDDC", distribution.Cook.GenerateDDC ? "true" : "false" },
            };
            foreach (var platform in availablePlatforms)
            {
                settings[$"With{platform}"] = distribution.Build.Platforms.Contains(platform) ? "true" : "false";
            }

            var editorPlatforms = distribution.Build.EditorPlatforms;
            if (editorPlatforms.Length == 0)
            {
                editorPlatforms = new[] { "Win64" };
            }

            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForEngine(),
                BuildGraphTarget = string.Join("+", editorPlatforms.Select(x => $"Make Installed Build {x}")),
                BuildGraphSettings = settings,
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = string.Empty,
                UETPath = _selfLocation.GetUetLocalLocation(),
                GlobalEnvironmentVariables = new Dictionary<string, string>(),
                ProjectFolderName = null,
                ArtifactExportPath = Environment.CurrentDirectory,
                MobileProvisions = distribution.MobileProvisions,
            };
        }

        private string[] FilterIncompatiblePlatforms(string[] platforms, bool localExecutor)
        {
            HashSet<string> newPlatforms;
            if (!localExecutor)
            {
                newPlatforms = platforms.ToHashSet();
            }
            else if (OperatingSystem.IsWindows())
            {
                newPlatforms = platforms
                    .Where(x => !x.Equals("Mac", StringComparison.OrdinalIgnoreCase) && !x.Equals("IOS", StringComparison.OrdinalIgnoreCase))
                    .ToHashSet();
            }
            else
            {
                newPlatforms = platforms
                    .Where(x => x.Equals("Mac", StringComparison.OrdinalIgnoreCase) || x.Equals("IOS", StringComparison.OrdinalIgnoreCase))
                    .ToHashSet();
            }

            var excludedViaEnvironment = (Environment.GetEnvironmentVariable("UET_RUNTIME_EXCLUDED_PLATFORMS") ?? string.Empty)
                .Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var excluded in excludedViaEnvironment)
            {
                if (newPlatforms.Contains(excluded))
                {
                    _logger.LogWarning($"Excluding platform '{excluded}' because it is set in the environment variable UET_RUNTIME_EXCLUDED_PLATFORMS.");
                }
                newPlatforms.Remove(excluded);
            }

            return newPlatforms.ToArray();
        }

        private async Task<(string nodeInclude, string macroInclude)> WriteDynamicBuildGraphIncludeAsync(
            BuildGraphEnvironment env,
            bool localExecutor,
            object buildConfig,
            object distribution,
            string[]? executeTests,
            string[]? executeDeployments)
        {
            var sharedStorageAbsolutePath = OperatingSystem.IsWindows() ?
                env.Windows.SharedStorageAbsolutePath :
                env.Mac!.SharedStorageAbsolutePath;
            Directory.CreateDirectory(sharedStorageAbsolutePath);

            var nodeFilename = $"DynamicBuildGraph-{Environment.ProcessId}.Nodes.xml";
            var macroFilename = $"DynamicBuildGraph-{Environment.ProcessId}.Macros.xml";

            using (var stream = new FileStream(Path.Combine(sharedStorageAbsolutePath, nodeFilename), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await _dynamicBuildGraphIncludeWriter.WriteBuildGraphNodeInclude(
                    stream,
                    localExecutor,
                    buildConfig,
                    distribution,
                    executeTests,
                    executeDeployments).ConfigureAwait(false);
            }
            await _worldPermissionApplier.GrantEveryonePermissionAsync(Path.Combine(sharedStorageAbsolutePath, nodeFilename), CancellationToken.None).ConfigureAwait(false);

            using (var stream = new FileStream(Path.Combine(sharedStorageAbsolutePath, macroFilename), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await _dynamicBuildGraphIncludeWriter.WriteBuildGraphMacroInclude(
                    stream,
                    localExecutor,
                    buildConfig,
                    distribution).ConfigureAwait(false);
            }
            await _worldPermissionApplier.GrantEveryonePermissionAsync(Path.Combine(sharedStorageAbsolutePath, macroFilename), CancellationToken.None).ConfigureAwait(false);

            _bugReportCollector?.CollectFileForBugReport(
                Path.Combine(sharedStorageAbsolutePath, nodeFilename),
                nodeFilename);
            _bugReportCollector?.CollectFileForBugReport(
                Path.Combine(sharedStorageAbsolutePath, macroFilename),
                macroFilename);

            return ($"__SHARED_STORAGE_PATH__/{nodeFilename}", $"__SHARED_STORAGE_PATH__/{macroFilename}");
        }

        public async Task<BuildSpecification> BuildConfigPluginToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigPlugin pluginInfo,
            BuildConfigPluginDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            string[]? executeTests,
            string[]? executeDeployments,
            bool strictIncludes,
            bool localExecutor,
            bool isPluginRooted,
            string? commandlinePluginVersionName,
            long? commandlinePluginVersionNumber,
            bool skipPackaging)
        {
            // Determine folder that the plugin is in.
            string pluginImmediatePath, pluginBuildGraphPath;
            if (isPluginRooted || File.Exists(Path.Combine(repositoryRoot, $"{pluginInfo.PluginName}.uplugin")))
            {
                _logger.LogTrace("Plugin is in root directory.");
                pluginImmediatePath = repositoryRoot;
                pluginBuildGraphPath = "__REPOSITORY_ROOT__";
            }
            else
            {
                _logger.LogTrace("Plugin is within subdirectory.");
                pluginImmediatePath = Path.Combine(repositoryRoot, pluginInfo.PluginName);
                pluginBuildGraphPath = $"__REPOSITORY_ROOT__/{pluginInfo.PluginName}";
            }

            // Determine build matrix.
            var editorTargetPlatforms = FilterIncompatiblePlatforms((distribution.Build.Editor?.Platforms ?? new[] { BuildConfigPluginBuildEditorPlatform.Win64 }).Select(x =>
            {
                switch (x)
                {
                    case BuildConfigPluginBuildEditorPlatform.Win64:
                        return "Win64";
                    case BuildConfigPluginBuildEditorPlatform.Mac:
                        return "Mac";
                    case BuildConfigPluginBuildEditorPlatform.Linux:
                        return "Linux";
                    default:
                        throw new NotSupportedException();
                }
            }).ToArray(), localExecutor);
            var gameConfig = ComputeTargetConfig("Game", distribution.Build.Game, localExecutor);
            var clientConfig = ComputeTargetConfig("Client", distribution.Build.Client, localExecutor);
            var serverConfig = ComputeTargetConfig("Server", distribution.Build.Server, localExecutor);

            // Compute directories to clean.
            var cleanDirectories = new List<string>();
            foreach (var filespec in distribution.Clean?.Filespecs ?? Array.Empty<string>())
            {
                cleanDirectories.Add(filespec);
            }

            // If strict includes is turned on at the distribution level, enable it
            // regardless of the --strict-includes setting.
            var strictIncludesAtPluginLevel = distribution.Build?.StrictIncludes ?? false;

            // Compute packaging settings.
            var versioningType = BuildConfigPluginPackageType.None;
            bool? usePrecompiled = null;
            if (!skipPackaging && distribution.Package != null)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                if (distribution.Package.Marketplace.HasValue &&
                    !distribution.Package.Type.HasValue)
                {
                    _logger.LogWarning("The 'Package.Marketplace = true' setting is deprecated. Please use 'Package.Type = Marketplace' or 'Package.Type = Fab'.");
                    versioningType = BuildConfigPluginPackageType.Marketplace;
                }
#pragma warning restore CS0618 // Type or member is obsolete
                else if (distribution.Package.Type.HasValue)
                {
                    versioningType = distribution.Package.Type.Value;
                }
                usePrecompiled = distribution.Package.UsePrecompiled;
            }
            var versionInfo = await _versioning.ComputePluginVersionNameAndNumberAsync(
                engineSpec,
                versioningType,
                CancellationToken.None).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(commandlinePluginVersionName))
            {
                versionInfo.versionName = commandlinePluginVersionName;
            }
            if (commandlinePluginVersionNumber.HasValue)
            {
                versionInfo.versionNumber = commandlinePluginVersionNumber.Value.ToString(CultureInfo.InvariantCulture);
            }

            // Validate packaging settings. If the plugin has custom configuration files
            // but does not specify a filter file, then it's almost certainly misconfigured
            // as the plugin configuration files will not be included for distribution.
            var configPath = Path.Combine(pluginImmediatePath, "Config");
            if (Directory.Exists(configPath) &&
                Directory.GetFiles(configPath, "*.ini").Length > 0 &&
                distribution?.Package?.Filter == null &&
                versioningType != BuildConfigPluginPackageType.None)
            {
                throw new BuildMisconfigurationException("This plugin contains configuration files underneath Config/, but no filter file was specified for Package.Filter in BuildConfig.json. This almost certainly means the distribution is misconfigured, as plugin configuration files will not be included in the package unless you explicitly include them with a filter file.");
            }

            // Verify that the plugin does not contain source files that would exceed path limits
            // when submitting to the Marketplace/Fab.
            if (versioningType != BuildConfigPluginPackageType.Generic)
            {
                var sourceDirectory = new DirectoryInfo(Path.Combine(pluginImmediatePath, "Source"));
                if (sourceDirectory.Exists)
                {
                    if (!VerifySourceFilesDoNotExceedSubmissionPathLimit(pluginInfo.PluginName, sourceDirectory))
                    {
                        throw new BuildMisconfigurationException("This plugin contains source files that would exceed the path limit upon submission to the Marketplace or Fab. See above for details on which paths are too long.");
                    }
                }
            }

            // Write dynamic build includes for tests and deployments.
            var (scriptNodeIncludes, scriptMacroIncludes) = await WriteDynamicBuildGraphIncludeAsync(
                buildGraphEnvironment,
                localExecutor,
                pluginInfo,
                distribution!,
                executeTests,
                executeDeployments).ConfigureAwait(false);

            // Compute the Gauntlet config paths.
            var gauntletPaths = new List<string>();
            if (distribution!.Gauntlet != null)
            {
                foreach (var path in distribution.Gauntlet.ConfigFiles ?? Array.Empty<string>())
                {
                    gauntletPaths.Add(path);
                }
            }

            // Compute copyright header.
            var copyrightHeader = string.Empty;
            var copyrightExcludes = string.Empty;
            if (versioningType != BuildConfigPluginPackageType.Generic)
            {
                if (pluginInfo.Copyright == null)
                {
                    throw new BuildMisconfigurationException("You must configure the 'Copyright' section in BuildConfig.json to package for the Marketplace/Fab.");
                }
                else if (pluginInfo.Copyright.Header == null)
                {
                    throw new BuildMisconfigurationException("You must configure the 'Copyright.Header' value in BuildConfig.json to package for the Marketplace/Fab.");
                }
                else if (!pluginInfo.Copyright.Header.Contains("%Y", StringComparison.Ordinal))
                {
                    throw new BuildMisconfigurationException("The configured copyright header must have a %Y placeholder for the current year to package for the Marketplace/Fab.");
                }
                else
                {
                    copyrightHeader = pluginInfo.Copyright.Header.Replace("%Y", DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
                    if (pluginInfo.Copyright.ExcludePaths != null)
                    {
                        copyrightExcludes = string.Join(";", pluginInfo.Copyright.ExcludePaths);
                    }
                }
            }

            // Compute global environment variables.
            var globalEnvironmentVariables = new Dictionary<string, string>
            {
                { "BUILDING_FOR_REDISTRIBUTION", "true" },
            };
            if (distribution.EnvironmentVariables != null)
            {
                foreach (var kv in distribution.EnvironmentVariables)
                {
                    globalEnvironmentVariables[kv.Key] = kv.Value;
                }
            }

            // Use 'UsePrecompiled' setting if it exists, otherwise fallback to default behaviour of precompiled removal.
            var distributionUsesPrecompiled = usePrecompiled != null
                ? usePrecompiled.Value
                : versioningType == BuildConfigPluginPackageType.Generic;

            // Compute final settings for BuildGraph.
            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForPlugin(),
                BuildGraphTarget = "End",
                BuildGraphSettings = new Dictionary<string, string>
                {
                    // Environment options
                    { "UETPath", $"__UET_PATH__" },
                    { "UETGlobalArgs", _globalArgsProvider?.GlobalArgsString ?? string.Empty },
                    { "EnginePath", "__ENGINE_PATH__" },
                    { "TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                    { "ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { "PluginDirectory", pluginBuildGraphPath },
                    { "PluginName", pluginInfo.PluginName },
                    { "Distribution", distribution.Name },
                    { "ArtifactExportPath", "__ARTIFACT_EXPORT_PATH__" },

                    // Dynamic graph
                    { "ScriptNodeIncludes", scriptNodeIncludes },
                    { "ScriptMacroIncludes", scriptMacroIncludes },

                    // General options
                    { "IsUnrealEngine5", "true" },

                    // Clean options
                    { "CleanDirectories", string.Join(";", cleanDirectories) },

                    // Build options
                    { "ExecuteBuild", executeBuild ? "true" : "false" },
                    { "EditorTargetPlatforms", string.Join(";", editorTargetPlatforms) },
                    { "GameTargetPlatforms", gameConfig.TargetPlatforms },
                    { "ClientTargetPlatforms", clientConfig.TargetPlatforms },
                    { "ServerTargetPlatforms", serverConfig.TargetPlatforms },
                    { "GameConfigurations", gameConfig.Configurations },
                    { "ClientConfigurations", clientConfig.Configurations },
                    { "ServerConfigurations", serverConfig.Configurations },
                    { "MacPlatforms", $"IOS;Mac" },
                    { "StrictIncludes", strictIncludes || strictIncludesAtPluginLevel ? "true" : "false" },
                    { "EnginePrefix", "Unreal" },
                    { "StripDebugSymbols", (distribution.Build?.StripDebugSymbols ?? false) ? "true" : "false" },
                    { "AppleArchitectureOnly", (distribution.Build?.AppleArchitectureOnly ?? false) ? "true" : "false" },

                    // Package options
                    { "VersionNumber", versionInfo.versionNumber },
                    { "VersionName", versionInfo.versionName },
                    { "PackageFolder", distribution.Package?.OutputFolderName ?? "Packaged" },
                    { "PackageInclude", GetFilterInclude(repositoryRoot, distribution) },
                    { "PackageExclude", GetFilterExclude(repositoryRoot, distribution) },
                    { "PackageType", versioningType.ToString() },
                    { "DistributionUsesPrecompiled", distributionUsesPrecompiled ? "true" : "false" },
                    { "CopyrightHeader", copyrightHeader },
                    { "CopyrightExcludes", copyrightExcludes },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = repositoryRoot,
                UETPath = _selfLocation.GetUetLocalLocation(),
                GlobalEnvironmentVariables = globalEnvironmentVariables,
                ProjectFolderName = null,
                ArtifactExportPath = Environment.CurrentDirectory,
                MobileProvisions = distribution.MobileProvisions,
            };
        }

        public async Task<BuildSpecification> BuildConfigProjectToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigProject buildConfig,
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            string[]? executeTests,
            string[]? executeDeployments,
            bool strictIncludes,
            bool localExecutor,
            string? alternateStagingDirectory)
        {
            // Generate release version for project.
            var releaseVersion = _versioning.ComputeProjectReleaseVersion();

            // Determine build matrix.
            var editorTarget = distribution.Build.Editor?.Target ?? "UnrealEditor";
            var gameConfig = ComputeTargetConfig("Game", distribution.Build.Game, localExecutor);
            var clientConfig = ComputeTargetConfig("Client", distribution.Build.Client, localExecutor);
            var serverConfig = ComputeTargetConfig("Server", distribution.Build.Server, localExecutor);

            // Write dynamic build includes for tests and deployments.
            var (scriptNodeIncludes, scriptMacroIncludes) = await WriteDynamicBuildGraphIncludeAsync(
                buildGraphEnvironment,
                localExecutor,
                buildConfig,
                distribution,
                executeTests,
                executeDeployments).ConfigureAwait(false);

            // Compute final settings for BuildGraph.
            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForProject(),
                BuildGraphTarget = "End",
                BuildGraphSettings = new Dictionary<string, string>
                {
                    // Environment options
                    { "UETPath", $"__UET_PATH__" },
                    { "EnginePath", "__ENGINE_PATH__" },
                    { "TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                    { "ProjectRoot", $"__REPOSITORY_ROOT__/{distribution.FolderName}" },
                    { "RepositoryRoot", $"__REPOSITORY_ROOT__" },
                    { "ArtifactExportPath", "__ARTIFACT_EXPORT_PATH__" },

                    // Dynamic graph
                    { "ScriptNodeIncludes", scriptNodeIncludes },
                    { "ScriptMacroIncludes", scriptMacroIncludes },

                    // General options
                    { "UProjectPath", $"__REPOSITORY_ROOT__/{distribution.FolderName}/{distribution.ProjectName}.uproject" },
                    { "ProjectName", distribution.ProjectName },
                    { "Distribution", distribution.Name },
                    { "IsUnrealEngine5", "true" },
                    { "IsUnrealEngineInstalled", "__UNREAL_ENGINE_IS_INSTALLED_BUILD__" },
                    { "Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) },

                    // Build options
                    { "ExecuteBuild", executeBuild ? "true" : "false" },
                    { "EditorTarget", editorTarget },
                    { "GameTargets", gameConfig.Targets },
                    { "ClientTargets", clientConfig.Targets },
                    { "ServerTargets", serverConfig.Targets },
                    { "GameTargetPlatforms", gameConfig.TargetPlatforms },
                    { "ClientTargetPlatforms", clientConfig.TargetPlatforms },
                    { "ServerTargetPlatforms", serverConfig.TargetPlatforms },
                    { "GameConfigurations", gameConfig.Configurations },
                    { "ClientConfigurations", clientConfig.Configurations },
                    { "ServerConfigurations", serverConfig.Configurations },
                    { "AndroidGameCookFlavors", gameConfig.CookFlavors },
                    { "AndroidClientCookFlavors", clientConfig.CookFlavors },
                    { "MacPlatforms", $"IOS;Mac" },
                    { "StrictIncludes", strictIncludes ? "true" : "false" },

                    // Stage options
                    { "StageDirectory", string.IsNullOrWhiteSpace(alternateStagingDirectory) ? $"__REPOSITORY_ROOT__/{distribution.FolderName}/Saved/StagedBuilds" : alternateStagingDirectory.Replace("__REPOSITORY_ROOT__", $"__REPOSITORY_ROOT__/{distribution.FolderName}", StringComparison.Ordinal) },

                    // Version options
                    { "ReleaseVersion", releaseVersion },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = repositoryRoot,
                UETPath = _selfLocation.GetUetLocalLocation(),
                ProjectFolderName = distribution.FolderName,
                ArtifactExportPath = Environment.CurrentDirectory,
                MobileProvisions = distribution.MobileProvisions,
            };
        }

        public async Task<BuildSpecification> PluginPathSpecToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes,
            string[] extraPlatforms,
            BuildConfigPluginPackageType packageType,
            string? commandlinePluginVersionName,
            long? commandlinePluginVersionNumber)
        {
            var targetPlatform = OperatingSystem.IsWindows() ? "Win64" : "Mac";
            var gameConfigurations = shipping ? "Shipping" : "Development";

            var versionInfo = await _versioning.ComputePluginVersionNameAndNumberAsync(engineSpec, packageType, CancellationToken.None).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(commandlinePluginVersionName))
            {
                versionInfo.versionName = commandlinePluginVersionName;
            }
            if (commandlinePluginVersionNumber.HasValue)
            {
                versionInfo.versionNumber = commandlinePluginVersionNumber.Value.ToString(CultureInfo.InvariantCulture);
            }

            // If building for the Marketplace/Fab, compute the copyright header
            // automatically from the .uplugin CreatedBy field.
            var copyrightHeader = string.Empty;
            if (packageType != BuildConfigPluginPackageType.Generic)
            {
                var pluginFile = JsonSerializer.Deserialize(
                    await File.ReadAllTextAsync(pathSpec.UPluginPath!).ConfigureAwait(false),
                    ProjectPluginFileJsonSerializerContext.Default.UPluginFile);
                if (string.IsNullOrWhiteSpace(pluginFile?.CreatedBy))
                {
                    _logger.LogWarning(".uplugin file is missing 'CreatedBy' field. Copyright headers set for Marketplace/Fab submission may not the Marketplace/Fab guidelines. Please set the 'CreatedBy' field or use a 'BuildConfig.json' to build this plugin.");
                    copyrightHeader = $"Copyright %Y. All Rights Reserved.";
                }
                else
                {
                    copyrightHeader = $"Copyright {pluginFile?.CreatedBy} %Y. All Rights Reserved.";
                }
            }

            // Verify that the plugin does not contain source files that would exceed path limits
            // when submitting to the Marketplace/Fab.
            if (packageType != BuildConfigPluginPackageType.Generic)
            {
                var sourceDirectory = new DirectoryInfo(Path.Combine(
                    new FileInfo(pathSpec.UPluginPath!).DirectoryName!,
                    "Source"));
                if (sourceDirectory.Exists)
                {
                    if (!VerifySourceFilesDoNotExceedSubmissionPathLimit(
                            Path.GetFileNameWithoutExtension(pathSpec.UPluginPath!),
                            sourceDirectory))
                    {
                        throw new BuildMisconfigurationException("This plugin contains source files that would exceed the path limit upon submission to the Marketplace/Fab. See above for details on which paths are too long.");
                    }
                }
            }

            // Determine default "distribution uses precompiled" flag. This inherits previous behaviour. To control
            // it independent of the package type, developers need to use a BuildConfig.json file.
            var distributionUsesPrecompiled = packageType == BuildConfigPluginPackageType.Generic;

            // Compute final settings for BuildGraph.
            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForPlugin(),
                BuildGraphTarget = "End",
                BuildGraphSettings = new Dictionary<string, string>
                {
                    // Environment options
                    { "UETPath", $"__UET_PATH__" },
                    { "UETGlobalArgs", _globalArgsProvider?.GlobalArgsString ?? string.Empty },
                    { "EnginePath", "__ENGINE_PATH__" },
                    { "TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                    { "ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { "PluginDirectory", $"__REPOSITORY_ROOT__" },
                    { "PluginName", Path.GetFileNameWithoutExtension(pathSpec.UPluginPath)! },
                    // @note: This is only used for naming the package ZIPs now.
                    { "Distribution", packageType.ToString() },
                    { "ArtifactExportPath", "__ARTIFACT_EXPORT_PATH__" },

                    // Dynamic graph
                    { "ScriptNodeIncludes", string.Empty },
                    { "ScriptMacroIncludes", string.Empty },

                    // General options
                    { "IsUnrealEngine5", "true" },

                    // Clean options
                    { "CleanDirectories", string.Empty },

                    // Build options
                    { "ExecuteBuild", "true" },
                    { "EditorTargetPlatforms", targetPlatform },
                    { "GameTargetPlatforms", string.Join(";", new[] { targetPlatform }.Concat(extraPlatforms)) },
                    { "GameConfigurations", gameConfigurations },
                    { "MacPlatforms", $"IOS;Mac" },
                    { "StrictIncludes", strictIncludes ? "true" : "false" },
                    { "EnginePrefix", "Unreal" },
                    { "StripDebugSymbols", "false" },
                    { "AppleArchitectureOnly", "false" },

                    // Package options
                    { "VersionNumber", versionInfo.versionNumber },
                    { "VersionName", versionInfo.versionName },
                    { "PackageFolder", packageType.ToString() },
                    { "PackageInclude", string.Empty },
                    { "PackageExclude", string.Empty },
                    { "PackageType", packageType.ToString() },
                    { "DistributionUsesPrecompiled", distributionUsesPrecompiled ? "true" : "false" },
                    { "CopyrightHeader", copyrightHeader },
                    { "CopyrightExcludes", string.Empty },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = pathSpec.DirectoryPath,
                UETPath = _selfLocation.GetUetLocalLocation(),
                GlobalEnvironmentVariables = new Dictionary<string, string>
                {
                    { "BUILDING_FOR_REDISTRIBUTION", "true" },
                },
                ProjectFolderName = null,
                ArtifactExportPath = Environment.CurrentDirectory,
                MobileProvisions = Array.Empty<BuildConfigMobileProvision>(),
            };
        }

        public BuildSpecification ProjectPathSpecToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping,
            bool strictIncludes,
            string[] extraPlatforms,
            string? alternateStagingDirectory)
        {
            // Generate release version for project.
            var releaseVersion = _versioning.ComputeProjectReleaseVersion();

            // Use heuristics to guess the targets for this build.
            string editorTarget;
            string gameTarget;
            if (Directory.Exists(Path.Combine(pathSpec.DirectoryPath, "Source")))
            {
                var files = Directory.GetFiles(Path.Combine(pathSpec.DirectoryPath, "Source"), "*.Target.cs");
                editorTarget = files.Where(x => x.EndsWith("Editor.Target.cs", StringComparison.Ordinal)).Select(x => Path.GetFileName(x)).First();
                editorTarget = editorTarget[..editorTarget.LastIndexOf(".Target.cs", StringComparison.Ordinal)];
                gameTarget = editorTarget[..editorTarget.LastIndexOf("Editor", StringComparison.Ordinal)];
            }
            else
            {
                editorTarget = "UnrealEditor";
                gameTarget = "UnrealGame";
            }

            var gameTargetPlatform = OperatingSystem.IsWindows() ? "Win64" : "Mac";
            var gameConfigurations = shipping ? "Shipping" : "Development";

            // Compute final settings for BuildGraph.
            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForProject(),
                BuildGraphTarget = "End",
                BuildGraphSettings = new Dictionary<string, string>
                {
                    // Environment options
                    { "UETPath", $"__UET_PATH__" },
                    { "EnginePath", "__ENGINE_PATH__" },
                    { "TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                    { "ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { "RepositoryRoot", $"__REPOSITORY_ROOT__" },
                    { "ArtifactExportPath", "__ARTIFACT_EXPORT_PATH__" },

                    // Dynamic graph
                    { "ScriptNodeIncludes", string.Empty },
                    { "ScriptMacroIncludes", string.Empty },

                    // General options
                    { "UProjectPath", $"__REPOSITORY_ROOT__/{Path.GetFileName(pathSpec.UProjectPath)}" },
                    { "Distribution", "None" },
                    { "IsUnrealEngine5", "true" },
                    { "IsUnrealEngineInstalled", "__UNREAL_ENGINE_IS_INSTALLED_BUILD__" },
                    { "Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) },

                    // Build options
                    { "ExecuteBuild", "true" },
                    { "EditorTarget", editorTarget },
                    { "GameTargets", gameTarget },
                    { "ClientTargets", string.Empty },
                    { "ServerTargets", string.Empty },
                    { "GameTargetPlatforms", string.Join(";", new[] { gameTargetPlatform }.Concat(extraPlatforms)) },
                    { "ClientTargetPlatforms", string.Empty },
                    { "ServerTargetPlatforms", string.Empty },
                    { "GameConfigurations", gameConfigurations },
                    { "ClientConfigurations", string.Empty },
                    { "ServerConfigurations", string.Empty },
                    { "AndroidGameCookFlavors", string.Empty },
                    { "AndroidClientCookFlavors", string.Empty },
                    { "MacPlatforms", $"IOS;Mac" },
                    { "StrictIncludes", strictIncludes ? "true" : "false" },

                    // Stage options
                    { "StageDirectory", string.IsNullOrWhiteSpace(alternateStagingDirectory) ? $"__REPOSITORY_ROOT__/Saved/StagedBuilds" : alternateStagingDirectory },

                    // Version options
                    { "ReleaseVersion", releaseVersion },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = pathSpec.DirectoryPath,
                UETPath = _selfLocation.GetUetLocalLocation(),
                ProjectFolderName = string.Empty,
                ArtifactExportPath = Environment.CurrentDirectory,
                MobileProvisions = Array.Empty<BuildConfigMobileProvision>(),
            };
        }
    }
}
