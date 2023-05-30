namespace UET.Commands.Build
{
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.BuildPipeline.Environment;
    using Redpoint.UET.BuildPipeline.Executors;
    using Redpoint.UET.Configuration.Engine;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;
    using System;
    using UET.Commands.EngineSpec;

    internal class DefaultBuildSpecificationGenerator : IBuildSpecificationGenerator
    {
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

        public BuildSpecification BuildConfigEngineToBuildSpec(BuildEngineSpecification engineSpec, BuildConfigEngineDistribution distribution)
        {
            throw new NotImplementedException();
        }

        public BuildSpecification BuildConfigPluginToBuildSpec(BuildEngineSpecification engineSpec, BuildConfigPluginDistribution distribution)
        {
            throw new NotImplementedException();
        }

        public BuildSpecification BuildConfigProjectToBuildSpec(
            BuildEngineSpecification engineSpec,
            BuildGraphEnvironment buildGraphEnvironment,
            BuildConfigProjectDistribution distribution,
            string repositoryRoot,
            bool executeBuild,
            bool strictIncludes,
            bool executeTests,
            bool executeDeployment)
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
                BuildGraphSettings = new Redpoint.UET.BuildPipeline.Environment.BuildGraphSettings
                {
                    // Environment options
                    { $"BuildScriptsPath", $"__REPOSITORY_ROOT__/BuildScripts" },
                    { $"BuildScriptsLibPath", $"__REPOSITORY_ROOT__/BuildScripts/Lib" },
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
            };
        }

        public BuildSpecification PluginPathSpecToBuildSpec(BuildEngineSpecification engineSpec, PathSpec pathSpec)
        {
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
                BuildGraphSettings = new Redpoint.UET.BuildPipeline.Environment.BuildGraphSettings
                {
                    // Environment options
                    { $"BuildScriptsPath", $"__REPOSITORY_ROOT__/BuildScripts" },
                    { $"BuildScriptsLibPath", $"__REPOSITORY_ROOT__/BuildScripts/Lib" },
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
            };
        }
    }
}
