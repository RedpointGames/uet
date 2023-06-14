using Microsoft.Extensions.DependencyInjection;
using System.CommandLine;
using Redpoint.PathResolution;
using Redpoint.ProcessExecution;
using Redpoint.UET.UAT;
using Redpoint.UET.BuildPipeline;
using Redpoint.UET.BuildPipeline.Executors;
using Redpoint.UET.BuildPipeline.BuildGraph;
using Redpoint.UET.Workspace;
using Redpoint.UET.Core;
using Microsoft.Extensions.Logging;
using Redpoint.MSBuildResolution;
using Redpoint.UET.BuildPipeline.Executors.Local;
using System.Diagnostics;
using Redpoint.Uefs.Protocol;

var enginePathOpt = new Option<DirectoryInfo>(
    name: "--engine-path",
    description: "The path to the Unreal Engine install.")
{
};
var engineTagOpt = new Option<string>(
    name: "--engine-tag",
    description: "The tag to the Unreal Engine package.")
{
};
var pluginPathOpt = new Option<DirectoryInfo>(
    name: "--plugin-path",
    description: "The path to the plugin to build.")
{
};
var projectPathOpt = new Option<DirectoryInfo>(
    name: "--project-path",
    description: "The path to the project to build.")
{
};

var rootCommand = new RootCommand("UAT build pipeline test. Internal use only.");
rootCommand.AddOption(enginePathOpt);
rootCommand.AddOption(engineTagOpt);
rootCommand.AddOption(pluginPathOpt);
rootCommand.AddOption(projectPathOpt);

rootCommand.SetHandler(async (context) =>
{
    var enginePath = context.ParseResult.GetValueForOption(enginePathOpt);
    var engineTag = context.ParseResult.GetValueForOption(engineTagOpt);
    if ((enginePath == null || !enginePath.Exists) &&
        string.IsNullOrEmpty(engineTag))
    {
        Console.Error.WriteLine("error: --engine-path must exist or --engine-tag must be set.");
        Environment.ExitCode = 1;
        return;
    }
    var pluginPath = context.ParseResult.GetValueForOption(pluginPathOpt);
    var projectPath = context.ParseResult.GetValueForOption(projectPathOpt);
    if ((pluginPath == null || !pluginPath.Exists) &&
        (projectPath == null || !projectPath.Exists))
    {
        Console.Error.WriteLine("error: either --plugin-path or --project-path must exist.");
        Environment.ExitCode = 1;
        return;
    }
    var isProject = !(projectPath == null || !projectPath.Exists);

    var services = new ServiceCollection();
    services.AddPathResolution();
    services.AddMSBuildPathResolution();
    services.AddProcessExecution();
    services.AddUefs();
    services.AddUETUAT();
    services.AddUETBuildPipeline();
    services.AddUETBuildPipelineExecutorsLocal();
    services.AddUETWorkspace();
    services.AddUETCore();

    var serviceProvider = services.BuildServiceProvider();

    var localBuildExecutor = serviceProvider.GetRequiredService<LocalBuildExecutorFactory>().CreateExecutor();

    BuildEngineSpecification buildEngineSpecification;
    if (!string.IsNullOrWhiteSpace(engineTag))
    {
        buildEngineSpecification = BuildEngineSpecification.ForUEFSPackageTag(engineTag);
    }
    else
    {
        buildEngineSpecification = BuildEngineSpecification.ForAbsolutePath(context.ParseResult.GetValueForOption(enginePathOpt)!.FullName);
    }

    BuildSpecification buildSpecification;
    if (isProject)
    {
        Directory.CreateDirectory(Path.Combine(projectPath!.FullName, "Saved", "SharedStorage"));

        var projectName = projectPath!.GetFiles("*.uproject").First().Name;
        buildSpecification = new BuildSpecification
        {
            Engine = buildEngineSpecification,
            BuildGraphScript = BuildGraphScriptSpecification.ForProject(),
            BuildGraphTarget = "End",
            BuildGraphSettings = new Dictionary<string, string>
            {
                { $"UETPath", Process.GetCurrentProcess().MainModule!.FileName },
                { $"TempPath", $"__REPOSITORY_ROOT__/.uet/tmp" },
                { $"ProjectRoot", $"__REPOSITORY_ROOT__" },
                { $"RepositoryRoot", $"__REPOSITORY_ROOT__" },
                { $"UProjectPath", $"__REPOSITORY_ROOT__/__PROJECT_FILENAME__" },
                { $"Distribution", $"Default" },
                { $"PrepareCustomCompileScripts", $"" },
                { "IsUnrealEngine5", "true" },
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
            BuildGraphEnvironment = new Redpoint.UET.BuildPipeline.Environment.BuildGraphEnvironment
            {
                PipelineId = string.Empty,
                Windows = new Redpoint.UET.BuildPipeline.Environment.BuildGraphWindowsEnvironment
                {
                    SharedStorageAbsolutePath = $"{Path.Combine(projectPath.FullName, "Saved", "SharedStorage").TrimEnd('\\')}\\",
                    SdksPath = null,
                },
                Mac = new Redpoint.UET.BuildPipeline.Environment.BuildGraphMacEnvironment
                {
                    // @note: This executable is for internal testing, so it doesn't support macOS.
                    SharedStorageAbsolutePath = null!,
                    SdksPath = null,
                }
            },
            BuildGraphRepositoryRoot = projectPath.FullName,
            BuildGraphSettingReplacements = new Dictionary<string, string>
            {
                { "__PROJECT_FILENAME__", projectName },
            },
            UETPath = Process.GetCurrentProcess().MainModule!.FileName,
            ProjectFolderName = string.Empty,
        };
    }
    else
    {
        throw new NotSupportedException();
    }

    var result = await localBuildExecutor.ExecuteBuildAsync(
        buildSpecification,
        new LoggerBasedBuildExecutionEvents(serviceProvider.GetRequiredService<ILogger<LoggerBasedBuildExecutionEvents>>()),
        CaptureSpecification.Passthrough,
        context.GetCancellationToken());
    Environment.ExitCode = result;
});

return await rootCommand.InvokeAsync(args);