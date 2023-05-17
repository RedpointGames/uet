namespace Redpoint.UET.BuildPipeline.Tests
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.BuildPipeline.BuildGraph;
    using Redpoint.UET.UAT;
    using System.Diagnostics;

    public class BuildGraphGeneratorTests
    {
        [Fact]
        public async void CanGenerateBuildGraphForProject()
        {
            var enginePath = Environment.GetEnvironmentVariable("UET_ENGINE_PATH") ?? @"E:\EpicGames\UE_5.2";
            var projectPath = Environment.GetEnvironmentVariable("UET_PROJECT_PATH") ?? @"C:\Work\examples\EOS_CPlusPlus";
            Skip.IfNot(Directory.Exists(enginePath), $"Engine must exist at {enginePath} for this test to run.");
            Skip.IfNot(Directory.Exists(projectPath), $"Project must exist at {projectPath} for this test to run.");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUETUAT();
            services.AddUETBuildPipeline();

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetRequiredService<IBuildGraphGenerator>();

            var buildGraph = await generator.GenerateGraphAsync(
                @"E:\EpicGames\UE_5.2",
                BuildGraphScriptSpecification.ForProject(),
                "End",
                new[]
                {
                    $"-set:BuildScriptsPath={projectPath}/BuildScripts",
                    $"-set:BuildScriptsLibPath={projectPath}/BuildScripts/Lib",
                    $"-set:TempPath={projectPath}/BuildScripts/Temp",
                    $"-set:ProjectRoot={projectPath}",
                    $"-set:RepositoryRoot={projectPath}",
                    $"-set:UProjectPath={projectPath}/ExampleOSS.uproject",
                    $"-set:Distribution=Default",
                    $"-set:PrepareCustomCompileScripts=",
                    $"-set:ExecuteBuild=true",
                    $"-set:EditorTarget=ExampleOSSEditor",
                    $"-set:GameTargets=ExampleOSS",
                    $"-set:ClientTargets=",
                    $"-set:ServerTargets=",
                    $"-set:GameTargetPlatforms=Win64",
                    $"-set:ClientTargetPlatforms=",
                    $"-set:ServerTargetPlatforms=",
                    $"-set:GameConfigurations=DebugGame",
                    $"-set:ClientConfigurations=",
                    $"-set:ServerConfigurations=",
                    $"-set:MacPlatforms=IOS;Mac",
                    $"-set:StrictIncludes=false",
                    $"-set:StageDirectory={projectPath}/Saved/StagedBuilds",
                    $"-set:ExecuteTests=false",
                    $"-set:GauntletTests=",
                    $"-set:CustomTests=",
                    $"-set:DeploymentSteam=",
                    $"-set:DeploymentCustom=",
                },
                CaptureSpecification.Passthrough,
                CancellationToken.None);

            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Build");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Cook");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Pak and Stage");
            Assert.Contains(buildGraph.Groups, x => x.Name == "Windows Tag");
        }

        [Fact]
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
            services.AddUETUAT();
            services.AddUETBuildPipeline();

            var serviceProvider = services.BuildServiceProvider();
            var generator = serviceProvider.GetRequiredService<IBuildGraphGenerator>();

            var buildGraph = await generator.GenerateGraphAsync(
                @"E:\EpicGames\UE_5.2",
                BuildGraphScriptSpecification.ForPlugin(),
                "End",
                new[]
                {
                    $"-set:BuildScriptsPath={pluginPath}/BuildScripts",
                    $"-set:BuildScriptsLibPath={pluginPath}/BuildScripts/Lib",
                    $"-set:TempPath={pluginPath}/BuildScripts/Temp",
                    $"-set:ProjectRoot={pluginPath}",
                    $"-set:PluginDirectory={pluginPath}/OnlineSubsystemRedpointEOS",
                    $"-set:PluginName=OnlineSubsystemRedpointEOS",
                    $"-set:Distribution=Default",
                    $"-set:IsUnrealEngine5=true",
                    $"-set:CleanDirectories=",
                    $"-set:PrepareCustomAssembleFinalizeScripts=",
                    $"-set:PrepareCustomCompileScripts=",
                    $"-set:PrepareCustomTestScripts=",
                    $"-set:ExecuteBuild=true",
                    $"-set:EditorTargetPlatforms=Win64",
                    $"-set:GameTargetPlatforms=Win64",
                    $"-set:GameConfigurations=DebugGame",
                    $"-set:ClientTargetPlatforms=",
                    $"-set:ClientConfigurations=",
                    $"-set:ServerTargetPlatforms=",
                    $"-set:ServerConfigurations=",
                    $"-set:MacPlatforms=IOS;Mac",
                    $"-set:StrictIncludes=false",
                    $"-set:Allow2019=false",
                    $"-set:EnginePrefix=Unreal",
                    $"-set:ExecutePackage=false",
                    $"-set:VersionNumber=10000",
                    $"-set:VersionName=2023.05.17",
                    $"-set:PackageName=Packaged",
                    $"-set:PackageInclude=",
                    $"-set:PackageExclude=",
                    $"-set:IsForMarketplaceSubmission=false",
                    $"-set:CopyrightHeader=",
                    $"-set:CopyrightExcludes=",
                    $"-set:ExecuteTests=false",
                    $"-set:AutomationTests=",
                    $"-set:GauntletTests=",
                    $"-set:CustomTests=",
                    $"-set:DownstreamTests=",
                    $"-set:GauntletGameTargetPlatforms=",
                    $"-set:GauntletConfigPaths=",
                    $"-set:DeploymentBackblazeB2=",
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