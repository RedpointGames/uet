namespace UET.Commands.Build
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Environment;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System;
    using System.Linq;
    using UET.Commands.EngineSpec;
    using UET.Services;

    internal class DefaultBuildSpecificationGenerator : IBuildSpecificationGenerator
    {
        private readonly ILogger<DefaultBuildSpecificationGenerator> _logger;
        private readonly ISelfLocation _selfLocation;
        private readonly IVersioning _versioning;

        public DefaultBuildSpecificationGenerator(
            ILogger<DefaultBuildSpecificationGenerator> logger,
            ISelfLocation selfLocation,
            IVersioning versioning)
        {
            _logger = logger;
            _selfLocation = selfLocation;
            _versioning = versioning;
        }

        private struct TargetConfig
        {
            public required string Targets;
            public required string TargetPlatforms;
            public required string Configurations;
        }

        private TargetConfig ComputeTargetConfig(string name, BuildConfigProjectBuildTarget? target)
        {
            if (target == null)
            {
                return new TargetConfig
                {
                    Targets = string.Empty,
                    TargetPlatforms = string.Empty,
                    Configurations = string.Empty,
                };
            }

            var targets = target.Targets ?? new[] { $"Unreal{name}" };
            var targetPlatforms = target.Platforms;
            var configurations = target.Configurations ?? new[] { "Development", "Shipping" };

            return new TargetConfig
            {
                Targets = string.Join(";", targets),
                TargetPlatforms = string.Join(";", targetPlatforms),
                Configurations = string.Join(";", configurations),
            };
        }

        private TargetConfig ComputeTargetConfig(string name, BuildConfigPluginBuildTarget? target)
        {
            if (target == null)
            {
                return new TargetConfig
                {
                    Targets = string.Empty,
                    TargetPlatforms = string.Empty,
                    Configurations = string.Empty,
                };
            }

            var targets = new[] { $"Unreal{name}" };
            var targetPlatforms = target.Platforms;
            var configurations = target.Configurations ?? new[] { "Development", "Shipping" };

            return new TargetConfig
            {
                Targets = string.Join(";", targets),
                TargetPlatforms = string.Join(";", targetPlatforms),
                Configurations = string.Join(";", configurations),
            };
        }

        private string GetFilterInclude(
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
                    rawFilterRule.StartsWith(";") ||
                    rawFilterRule.StartsWith("-") ||
                    rawFilterRule.Trim().Length == 0)
                {
                    continue;
                }
                filterRules.Add(rawFilterRule);
            }
            return string.Join(";", filterRules);
        }

        private string GetFilterExclude(
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
                    rawFilterRule.StartsWith(";") ||
                    !rawFilterRule.StartsWith("-") ||
                    rawFilterRule.Trim().Length == 0)
                {
                    continue;
                }
                filterRules.Add(rawFilterRule.Substring(1));
            }
            return string.Join(";", filterRules);
        }

        public BuildSpecification BuildConfigEngineToBuildSpec(BuildEngineSpecification engineSpec, BuildConfigEngineDistribution distribution)
        {
            throw new NotImplementedException();
        }

        public async Task<BuildSpecification> BuildConfigPluginToBuildSpecAsync(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigPluginDistribution distribution,
            BuildConfigPlugin pluginInfo,
            string repositoryRoot,
            bool executeBuild,
            bool executePackage,
            bool executeTests,
            bool executeDeployment,
            bool strictIncludes)
        {
            // Determine build matrix.
            var editorTargetPlatforms = (distribution.Build.Editor?.Platforms ?? new[] { BuildConfigPluginBuildEditorPlatform.Win64 }).Select(x =>
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
            }).ToArray();
            var gameConfig = ComputeTargetConfig("Game", distribution.Build.Game);

            // Compute directories to clean.
            var cleanDirectories = new List<string>();
            foreach (var filespec in distribution.Clean?.Filespecs ?? new string[0])
            {
                cleanDirectories.Add(filespec);
            }

            // Compute prepare scripts.
            var prepareCustomAssembleFinalizeScripts = new List<string>();
            var prepareCustomCompileScripts = new List<string>();
            var prepareCustomTestScripts = new List<string>();
            var prepareCustomBuildGraphScripts = new List<string>();
            if (distribution.Prepare != null)
            {
                foreach (var prepare in distribution.Prepare)
                {
                    if (prepare.Type == BuildConfigPluginPrepareType.Custom &&
                        prepare.Custom != null &&
                        !string.IsNullOrWhiteSpace(prepare.Custom.ScriptPath))
                    {
                        if (prepare.RunBefore.Contains(BuildConfigPluginPrepareRunBefore.AssembleFinalize))
                        {
                            prepareCustomAssembleFinalizeScripts.Add(prepare.Custom.ScriptPath);
                        }
                        if (prepare.RunBefore.Contains(BuildConfigPluginPrepareRunBefore.Compile))
                        {
                            prepareCustomCompileScripts.Add(prepare.Custom.ScriptPath);
                        }
                        if (prepare.RunBefore.Contains(BuildConfigPluginPrepareRunBefore.Test))
                        {
                            prepareCustomTestScripts.Add(prepare.Custom.ScriptPath);
                        }
                        if (prepare.RunBefore.Contains(BuildConfigPluginPrepareRunBefore.BuildGraph))
                        {
                            prepareCustomBuildGraphScripts.Add(prepare.Custom.ScriptPath);
                        }
                    }
                }
            }

            // Compute packaging settings.
            var isForMarketplaceSubmission = distribution.Package != null &&
                (distribution.Package.Marketplace ?? false);
            var versionInfo = await _versioning.ComputeVersionNameAndNumberAsync(engineSpec, true, CancellationToken.None);

            // Validate packaging settings. If the plugin has custom configuration files
            // but does not specify a filter file, then it's almost certainly misconfigured
            // as the plugin configuration files will not be included for distribution.
            var configPath = Path.Combine(repositoryRoot, pluginInfo.PluginName, "Config");
            if (Directory.Exists(configPath) &&
                Directory.GetFiles(configPath, "*.ini").Length > 0 &&
                distribution?.Package?.Filter == null)
            {
                throw new BuildMisconfigurationException("This plugin contains configuration files underneath Config/, but no filter file was specified for Package.Filter in BuildConfig.json. This almost certainly means the distribution is misconfigured, as plugin configuration files will not be included in the package unless you explicitly include them with a filter file.");
            }

            // Compute automation tests.
            var automationTests = new List<string>();
            // @todo

            // Determine Gauntlet tasks.
            var gauntletTests = new List<string>();
            var gauntletPlatforms = new List<string>();
            // @todo

            // Compute the Gauntlet config paths.
            var gauntletPaths = new List<string>();
            // @todo

            // Compute custom tests.
            var customTests = new List<string>();
            // @todo

            // Compute downstream tests.
            var downstreamTests = new List<string>();
            // @todo

            // Compute deployment tasks.
            var deploymentBackblazeB2 = new List<string>();
            // @todo

            // Compute copyright header.
            var copyrightHeader = string.Empty;
            var copyrightExcludes = string.Empty;
            if (isForMarketplaceSubmission)
            {
                if (pluginInfo.Copyright == null)
                {
                    throw new BuildMisconfigurationException("You must configure the 'Copyright' section in BuildConfig.json to package for the Marketplace.");
                }
                else if (pluginInfo.Copyright.Header == null)
                {
                    throw new BuildMisconfigurationException("You must configure the 'Copyright.Header' value in BuildConfig.json to package for the Marketplace.");
                }
                else if (!pluginInfo.Copyright.Header.Contains("%Y"))
                {
                    throw new BuildMisconfigurationException("The configured copyright header must have a %Y placeholder for the current year to package for the Marketplace.");
                }
                else
                {
                    copyrightHeader = pluginInfo.Copyright.Header.Replace("%Y", DateTime.UtcNow.Year.ToString());
                    if (pluginInfo.Copyright.ExcludePaths != null)
                    {
                        copyrightExcludes = string.Join(";", pluginInfo.Copyright.ExcludePaths);
                    }
                }
            }

            // Compute final settings for BuildGraph.
            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForPlugin(),
                BuildGraphTarget = "End",
                BuildGraphSettings = new Dictionary<string, string>
                {
                    // Environment options
                    { $"UETPath", $"__UET_PATH__" },
                    { $"TempPath", $"__REPOSITORY_ROOT__/BuildScripts/Temp" },
                    { $"ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { $"PluginDirectory", $"__REPOSITORY_ROOT__/{pluginInfo.PluginName}" },
                    { $"PluginName", pluginInfo.PluginName },
                    { $"Distribution", distribution.Name },

                    // General options
                    { "IsUnrealEngine5", "true" },

                    // Clean options
                    { $"CleanDirectories", string.Join(";", cleanDirectories) },

                    // Prepare options
                    { $"PrepareCustomAssembleFinalizeScripts", string.Join(";", prepareCustomAssembleFinalizeScripts) },
                    { $"PrepareCustomCompileScripts", string.Join(";", prepareCustomCompileScripts) },
                    { $"PrepareCustomTestScripts", string.Join(";", prepareCustomTestScripts) },

                    // Build options
                    { $"ExecuteBuild", executeBuild ? "true" : "false" },
                    { $"EditorTargetPlatforms", string.Join(";", editorTargetPlatforms) },
                    { $"GameTargetPlatforms", gameConfig.TargetPlatforms },
                    { $"GameConfigurations", gameConfig.Configurations },
                    { $"MacPlatforms", $"IOS;Mac" },
                    { $"StrictIncludes", strictIncludes ? "true" : "false" },
                    { $"Allow2019", "false" },
                    { $"EnginePrefix", "Unreal" },

                    // Package options
                    { $"ExecutePackage", executePackage ? "true" : "false" },
                    { "VersionNumber", versionInfo.versionNumber },
                    { "VersionName", versionInfo.versionName },
                    { "PackageFolder", distribution.Package?.OutputFolderName ?? "Packaged" },
                    { "PackageInclude", GetFilterInclude(repositoryRoot, distribution) },
                    { "PackageExclude", GetFilterExclude(repositoryRoot, distribution) },
                    { "IsForMarketplaceSubmission", isForMarketplaceSubmission ? "true" : "false" },
                    { "CopyrightHeader", copyrightHeader },
                    { "CopyrightExcludes", copyrightExcludes },

                    // Test options
                    { $"ExecuteTests", executeTests ? "true" : "false" },
                    { $"AutomationTests", string.Join("+", automationTests) },
                    { $"GauntletTests", string.Join("+", gauntletTests) },
                    { $"CustomTests", string.Join("+", customTests) },
                    { $"DownstreamTests", string.Join("+", downstreamTests) },
                    { $"GauntletGameTargetPlatforms", string.Join(";", gauntletPlatforms) },
                    { $"GauntletConfigPaths", string.Join(";", gauntletPaths) },

                    // Deploy options
                    { $"DeploymentBackblazeB2", string.Join("+", deploymentBackblazeB2) },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = repositoryRoot,
                BuildGraphPreparationScripts = prepareCustomBuildGraphScripts,
                UETPath = _selfLocation.GetUETLocalLocation(),
                GlobalEnvironmentVariables = new Dictionary<string, string>
                {
                    { "BUILDING_FOR_REDISTRIBUTION", "true" },
                }
            };
        }

        public BuildSpecification BuildConfigProjectToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            bool executeTests,
            bool executeDeployment,
            bool strictIncludes)
        {
            // Determine build matrix.
            var editorTarget = distribution.Build.Editor?.Target ?? "UnrealEditor";
            var gameConfig = ComputeTargetConfig("Game", distribution.Build.Game);
            var clientConfig = ComputeTargetConfig("Client", distribution.Build.Client);
            var serverConfig = ComputeTargetConfig("Server", distribution.Build.Server);

            // Compute prepare scripts.
            var prepareCustomCompileScripts = new List<string>();
            var prepareCustomBuildGraphScripts = new List<string>();
            if (distribution.Prepare != null)
            {
                foreach (var prepare in distribution.Prepare)
                {
                    if (prepare.Type == BuildConfigProjectPrepareType.Custom &&
                        prepare.Custom != null &&
                        !string.IsNullOrWhiteSpace(prepare.Custom.ScriptPath))
                    {
                        if (prepare.RunBefore.Contains(BuildConfigProjectPrepareRunBefore.Compile))
                        {
                            prepareCustomCompileScripts.Add(prepare.Custom.ScriptPath);
                        }
                        if (prepare.RunBefore.Contains(BuildConfigProjectPrepareRunBefore.BuildGraph))
                        {
                            prepareCustomBuildGraphScripts.Add(prepare.Custom.ScriptPath);
                        }
                    }
                }
            }

            // Compute custom tests.
            var customTests = new List<string>();
            // @todo

            // Determine Gauntlet tasks.
            var gauntletTests = new List<string>();
            // @todo

            // Compute deployment tasks.
            var deploymentSteam = new List<string>();
            var deploymentCustom = new List<string>();
            if (executeDeployment)
            {
                // @todo
            }

            // Compute final settings for BuildGraph.
            return new BuildSpecification
            {
                Engine = engineSpec,
                BuildGraphScript = BuildGraphScriptSpecification.ForProject(),
                BuildGraphTarget = "End",
                BuildGraphSettings = new Dictionary<string, string>
                {
                    // Environment options
                    { $"UETPath", $"__UET_PATH__" },
                    { $"TempPath", $"__REPOSITORY_ROOT__/BuildScripts/Temp" },
                    { $"ProjectRoot", $"__REPOSITORY_ROOT__/{distribution.FolderName}" },
                    { $"RepositoryRoot", $"__REPOSITORY_ROOT__" },

                    // General options
                    { $"UProjectPath", $"__REPOSITORY_ROOT__/{distribution.FolderName}/{distribution.ProjectName}.uproject" },
                    { $"Distribution", distribution.Name },
                    { "IsUnrealEngine5", "true" },

                    // Prepare options
                    { $"PrepareCustomCompileScripts", string.Join(";", prepareCustomCompileScripts) },

                    // Build options
                    { $"ExecuteBuild", executeBuild ? "true" : "false" },
                    { $"EditorTarget", editorTarget },
                    { $"GameTargets", gameConfig.Targets },
                    { $"ClientTargets", clientConfig.Targets },
                    { $"ServerTargets", serverConfig.Targets },
                    { $"GameTargetPlatforms", gameConfig.TargetPlatforms },
                    { $"ClientTargetPlatforms", clientConfig.TargetPlatforms },
                    { $"ServerTargetPlatforms", serverConfig.TargetPlatforms },
                    { $"GameConfigurations", gameConfig.Configurations },
                    { $"ClientConfigurations", clientConfig.Configurations },
                    { $"ServerConfigurations", serverConfig.Configurations },
                    { $"MacPlatforms", $"IOS;Mac" },
                    { $"StrictIncludes", strictIncludes ? "true" : "false" },

                    // Stage options
                    { $"StageDirectory", $"__REPOSITORY_ROOT__/{distribution.FolderName}/Saved/StagedBuilds" },

                    // Test options
                    { $"ExecuteTests", executeTests ? "true" : "false" },
                    { $"GauntletTests", string.Join("+", gauntletTests) },
                    { $"CustomTests", string.Join("+", customTests) },

                    // Deploy options
                    { $"DeploymentSteam", string.Join("+", deploymentSteam) },
                    { $"DeploymentCustom", string.Join("+", deploymentCustom) },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = repositoryRoot,
                BuildGraphPreparationScripts = prepareCustomBuildGraphScripts,
                UETPath = _selfLocation.GetUETLocalLocation(),
            };
        }

        public BuildSpecification PluginPathSpecToBuildSpec(BuildEngineSpecification engineSpec, PathSpec pathSpec)
        {
            // @note: Must set BUILDING_FOR_REDISTRIBUTION="true" in GlobalEnvironmentVariables
            throw new NotImplementedException();
        }

        public BuildSpecification ProjectPathSpecToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            PathSpec pathSpec,
            bool shipping)
        {
            // Use heuristics to guess the targets for this build.
            string editorTarget;
            string gameTarget;
            if (Directory.Exists(Path.Combine(pathSpec.DirectoryPath, "Source")))
            {
                var files = Directory.GetFiles(Path.Combine(pathSpec.DirectoryPath, "Source"), "*.Target.cs");
                editorTarget = files.Where(x => x.EndsWith("Editor.Target.cs")).Select(x => Path.GetFileName(x)).First();
                editorTarget = editorTarget.Substring(0, editorTarget.LastIndexOf(".Target.cs"));
                gameTarget = editorTarget.Substring(0, editorTarget.LastIndexOf("Editor"));
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
                    { $"UETPath", $"__UET_PATH__" },
                    { $"TempPath", $"__REPOSITORY_ROOT__/BuildScripts/Temp" },
                    { $"ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { $"RepositoryRoot", $"__REPOSITORY_ROOT__" },

                    // General options
                    { $"UProjectPath", $"__REPOSITORY_ROOT__/{Path.GetFileName(pathSpec.UProjectPath)}" },
                    { $"Distribution", "None" },
                    { "IsUnrealEngine5", "true" },

                    // Prepare options
                    { $"PrepareCustomCompileScripts", string.Empty },

                    // Build options
                    { $"ExecuteBuild", "true" },
                    { $"EditorTarget", editorTarget },
                    { $"GameTargets", gameTarget },
                    { $"ClientTargets", string.Empty },
                    { $"ServerTargets", string.Empty },
                    { $"GameTargetPlatforms", gameTargetPlatform },
                    { $"ClientTargetPlatforms", string.Empty },
                    { $"ServerTargetPlatforms", string.Empty },
                    { $"GameConfigurations", gameConfigurations },
                    { $"ClientConfigurations", string.Empty },
                    { $"ServerConfigurations", string.Empty },
                    { $"MacPlatforms", $"IOS;Mac" },
                    { $"StrictIncludes", "false" },

                    // Stage options
                    { $"StageDirectory", $"__REPOSITORY_ROOT__/Saved/StagedBuilds" },

                    // Test options
                    { $"ExecuteTests", "false" },
                    { $"GauntletTests", string.Empty },
                    { $"CustomTests", string.Empty },

                    // Deploy options
                    { $"DeploymentSteam", string.Empty },
                    { $"DeploymentCustom", string.Empty },
                },
                BuildGraphEnvironment = buildGraphEnvironment,
                BuildGraphRepositoryRoot = pathSpec.DirectoryPath,
                UETPath = _selfLocation.GetUETLocalLocation(),
            };
        }
    }
}
