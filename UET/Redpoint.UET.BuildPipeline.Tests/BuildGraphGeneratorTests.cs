namespace Redpoint.UET.BuildPipeline.Tests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.UAT;
    using Redpoint.MSBuildResolution;
    using Redpoint.OpenGE.ProcessExecution;

    public class BuildGraphGeneratorTests
    {
        [SkippableFact]
        public async void CanGenerateBuildGraphForProject()
        {
            var enginePath = Environment.GetEnvironmentVariable("UET_ENGINE_PATH") ?? @"E:\EpicGames\UE_5.2";
            var projectPath = Environment.GetEnvironmentVariable("UET_PROJECT_PATH") ?? @"C:\Work\examples\EOS_CPlusPlus";
            Skip.IfNot(Directory.Exists(enginePath), $"Engine must exist at {enginePath} for this test to run.");
            Skip.IfNot(Directory.Exists(projectPath), $"Project must exist at {projectPath} for this test to run.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddOpenGEProcessExecution();
            services.AddProcessExecution();
            services.AddUETUAT();
            services.AddUETBuildPipeline();
            services.AddMSBuildPathResolution();

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetRequiredService<IBuildGraphExecutor>();

            var buildGraph = await generator.GenerateGraphAsync(
                @"E:\EpicGames\UE_5.2",
                projectPath,
                string.Empty,
                BuildGraphScriptSpecification.ForProject(),
                "End",
                new Dictionary<string, string>
                {
                    { $"UETPath", $"uet" },
                    { $"TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                    { $"ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { $"RepositoryRoot", $"__REPOSITORY_ROOT__" },
                    { $"UProjectPath", $"__REPOSITORY_ROOT__/ExampleOSS.uproject" },
                    { $"Distribution", $"Default" },
                    { $"PrepareCustomCompileScripts", $"" },
                    { $"ExecuteBuild", $"true" },
                    { $"EditorTarget", $"ExampleOSSEditor" },
                    { $"GameTargets", $"ExampleOSS" },
                    { $"ClientTargets", $"" },
                    { $"ServerTargets", $"" },
                    { $"GameTargetPlatforms", $"Win64" },
                    { $"ClientTargetPlatforms", $"" },
                    { $"ServerTargetPlatforms", $"" },
                    { $"GameConfigurations", $"DebugGame" },
                    { $"ClientConfigurations", $"" },
                    { $"ServerConfigurations", $"" },
                    { $"MacPlatforms", $"IOS;Mac" },
                    { $"StrictIncludes", $"false" },
                    { $"StageDirectory", $"__REPOSITORY_ROOT__/Saved/StagedBuilds" },
                    { $"ExecuteTests", $"false" },
                    { $"GauntletTests", $"" },
                    { $"CustomTests", $"" },
                    { $"DeploymentSteam", $"" },
                    { $"DeploymentCustom", $"" },
                },
                new Dictionary<string, string>
                {
                    { "__REPOSITORY_ROOT__", projectPath },
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);

            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Build");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Cook");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Pak and Stage");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Tag");
        }

        [SkippableFact]
        public async void CanGenerateBuildGraphForPlugin()
        {
            var enginePath = Environment.GetEnvironmentVariable("UET_ENGINE_PATH") ?? @"E:\EpicGames\UE_5.2";
            var pluginPath = Environment.GetEnvironmentVariable("UET_PLUGIN_PATH") ?? @"C:\Work\examples\EOS_CPlusPlus\Plugins\EOS";
            Skip.IfNot(Directory.Exists(enginePath), $"Engine must exist at {enginePath} for this test to run.");
            Skip.IfNot(Directory.Exists(pluginPath), $"Plugin must exist at {pluginPath} for this test to run.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddOpenGEProcessExecution();
            services.AddUETUAT();
            services.AddUETBuildPipeline();
            services.AddMSBuildPathResolution();

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetRequiredService<IBuildGraphExecutor>();

            var buildGraph = await generator.GenerateGraphAsync(
                @"E:\EpicGames\UE_5.2",
                pluginPath,
                string.Empty,
                BuildGraphScriptSpecification.ForPlugin(),
                "End",
                new Dictionary<string, string>
                {
                    { $"UETPath", $"uet" },
                    { $"TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                    { $"ProjectRoot", $"__REPOSITORY_ROOT__" },
                    { $"PluginDirectory", $"__REPOSITORY_ROOT__/OnlineSubsystemRedpointEOS" },
                    { $"PluginName", $"OnlineSubsystemRedpointEOS" },
                    { $"Distribution", $"Default" },
                    { $"IsUnrealEngine5", $"true" },
                    { $"CleanDirectories", $"" },
                    { $"PrepareCustomAssembleFinalizeScripts", $"" },
                    { $"PrepareCustomCompileScripts", $"" },
                    { $"PrepareCustomTestScripts", $"" },
                    { $"ExecuteBuild", $"true" },
                    { $"EditorTargetPlatforms", $"Win64" },
                    { $"GameTargetPlatforms", $"Win64" },
                    { $"GameConfigurations", $"DebugGame" },
                    { $"ClientTargetPlatforms", $"" },
                    { $"ClientConfigurations", $"" },
                    { $"ServerTargetPlatforms", $"" },
                    { $"ServerConfigurations", $"" },
                    { $"MacPlatforms", $"IOS;Mac" },
                    { $"StrictIncludes", $"false" },
                    { $"Allow2019", $"false" },
                    { $"EnginePrefix", $"Unreal" },
                    { $"ExecutePackage", $"false" },
                    { $"VersionNumber", $"10000" },
                    { $"VersionName", $"2023.05.17" },
                    { $"PackageName", $"Packaged" },
                    { $"PackageInclude", $"" },
                    { $"PackageExclude", $"" },
                    { $"IsForMarketplaceSubmission", $"false" },
                    { $"CopyrightHeader", $"" },
                    { $"CopyrightExcludes", $"" },
                    { $"ExecuteTests", $"false" },
                    { $"AutomationTests", $"" },
                    { $"GauntletTests", $"" },
                    { $"CustomTests", $"" },
                    { $"DownstreamTests", $"" },
                    { $"GauntletGameTargetPlatforms", $"" },
                    { $"GauntletConfigPaths", $"" },
                    { $"DeploymentBackblazeB2", $"" },
                },
                new Dictionary<string, string>
                {
                    { "__REPOSITORY_ROOT__", pluginPath },
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);

            Assert.Contains(buildGraph.Groups, x => x.Name == "Assemble Host Project");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Build Editor");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Build Game");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows End");
        }
    }
}