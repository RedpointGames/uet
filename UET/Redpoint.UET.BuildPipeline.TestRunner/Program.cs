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
using System.Text.RegularExpressions;

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
    services.AddUETUAT();
    services.AddUETBuildPipeline();
    services.AddUETWorkspace();
    services.AddUETCore();

    if (isProject)
    {
        services.AddSingleton<IPathProvider>(sp => new TestPathProvider(projectPath!.FullName));
    }
    else
    {
        services.AddSingleton<IPathProvider>(sp => new TestPathProvider(pluginPath!.FullName));
    }

    var serviceProvider = services.BuildServiceProvider();

    var localBuildExecutor = serviceProvider.GetRequiredService<LocalBuildExecutorFactory>().CreateExecutor();

    BuildEngineSpecification buildEngineSpecification;
    if (!string.IsNullOrWhiteSpace(engineTag))
    {
        buildEngineSpecification = BuildEngineSpecification.ForUEFSPackageTag(engineTag);
    }
    else
    {
        buildEngineSpecification = BuildEngineSpecification.ForPath(context.ParseResult.GetValueForOption(enginePathOpt)!.FullName);
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
            BuildGraphSettings = new Redpoint.UET.BuildPipeline.Environment.BuildGraphSettings
            {
                WindowsSettings = new Dictionary<string, string>
                {
                    { $"BuildScriptsPath", $"__REPOSITORY_ROOT__/BuildScripts" },
                    { $"BuildScriptsLibPath", $"__REPOSITORY_ROOT__/BuildScripts/Lib" },
                    { $"TempPath", $"__REPOSITORY_ROOT__/BuildScripts/Temp" },
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
                MacSettings = new Dictionary<string, string>()
                {
                    // @note: This executable is for internal testing, so it doesn't support macOS.
                }
            },
            BuildGraphEnvironment = new Redpoint.UET.BuildPipeline.Environment.BuildGraphEnvironment
            {
                PipelineId = string.Empty,
                Windows = new Redpoint.UET.BuildPipeline.Environment.BuildGraphWindowsEnvironment
                {
                    SharedStorageAbsolutePath = $"{Path.Combine(projectPath.FullName, "Saved", "SharedStorage").TrimEnd('\\')}\\",
                },
                Mac = new Redpoint.UET.BuildPipeline.Environment.BuildGraphMacEnvironment
                {
                    // @note: This executable is for internal testing, so it doesn't support macOS.
                    MacEnginePathOverride = null!,
                    SharedStorageAbsolutePath = null!,
                }
            },
            BuildGraphRepositoryRoot = projectPath.FullName,
            BuildGraphSettingReplacements = new Dictionary<string, string>
            {
                { "__PROJECT_FILENAME__", projectName },
            },
        };
    }
    else
    {
        throw new NotSupportedException();
    }

    var result = await localBuildExecutor.ExecuteBuildAsync(
        buildSpecification,
        new TestBuildExecutionEvents(serviceProvider.GetRequiredService<ILogger<TestBuildExecutionEvents>>()),
        CaptureSpecification.Passthrough,
        context.GetCancellationToken());
    Environment.ExitCode = result;
});

return await rootCommand.InvokeAsync(args);

class TestBuildExecutionEvents : IBuildExecutionEvents
{
    private readonly ILogger<TestBuildExecutionEvents> _logger;
    private static readonly Regex _warningRegex = new Regex("([^a-zA-Z0-9]|^)([Ww]arning)([^a-zA-Z0-9]|$)");
    private static readonly Regex _errorRegex = new Regex("([^a-zA-Z0-9]|^)([Ee]rror)([^a-zA-Z0-9]|$)");
    private static readonly Regex _successfulRegex = new Regex("([^a-zA-Z0-9]|^)([Ss][Uu][Cc][Cc][Ee][Ss][Ss][Ff]?[Uu]?[Ll]?)([^a-zA-Z0-9]|$)");

    public TestBuildExecutionEvents(ILogger<TestBuildExecutionEvents> logger)
    {
        _logger = logger;
    }

    public Task OnNodeFinished(string nodeName, BuildResultStatus resultStatus)
    {
        switch (resultStatus)
        {
            case BuildResultStatus.Success:
                _logger.LogInformation($"[{nodeName}] \x001B[32mPassed\x001B[0m");
                break;
            case BuildResultStatus.Failed:
                _logger.LogInformation($"[{nodeName}] \x001B[31mFailed\x001B[0m");
                break;
            case BuildResultStatus.NotRun:
                _logger.LogInformation($"[{nodeName}] \x001B[36mNot Run\x001B[0m");
                break;
        }
        return Task.CompletedTask;
    }

    public Task OnNodeOutputReceived(string nodeName, string[] lines)
    {
        foreach (var line in lines)
        {
            var highlightedLine = _warningRegex.Replace(line, m => $"{m.Groups[1].Value}\u001b[33m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
            highlightedLine = _errorRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[31m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
            highlightedLine = _successfulRegex.Replace(highlightedLine, m => $"{m.Groups[1].Value}\u001b[32m{m.Groups[2].Value}\u001b[0m{m.Groups[3].Value}");
            _logger.LogInformation($"[{nodeName}] {highlightedLine}");
        }
        return Task.CompletedTask;
    }

    public Task OnNodeStarted(string nodeName)
    {
        _logger.LogInformation($"[{nodeName}] \x001B[35mStarting...\x001B[0m");
        return Task.CompletedTask;
    }
}

class TestPathProvider : IPathProvider
{
    public TestPathProvider(string repositoryRoot)
    {
        RepositoryRoot = repositoryRoot;
    }

    public string RepositoryRoot { get; private init; }

    public string BuildScripts => throw new NotImplementedException();

    public string BuildScriptsLib => throw new NotImplementedException();

    public string BuildScriptsTemp => throw new NotImplementedException();
}
