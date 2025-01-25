namespace UET.Commands.Build
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using System.CommandLine.Invocation;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Engine;
    using Redpoint.Uet.BuildPipeline.Executors.Local;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.BuildPipeline.Executors.GitLab;
    using Redpoint.Uet.Core;
    using static Crayon.Output;
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Text.RegularExpressions;
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Concurrency;
    using k8s.Models;
    using UET.BuildConfig;
    using System.Runtime.InteropServices;
    using Microsoft.Extensions.Configuration;
    using System.Diagnostics;

    internal sealed class RunCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<DistributionSpec?> Distribution;
            public Option<bool> SupplyProject;
            public Argument<string> Target;
            public Argument<string[]> Arguments;

            public Options(
                IServiceProvider serviceProvider)
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file, a .uplugin file, or a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ZeroOrOne;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use for the build.",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, null),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;

                Distribution = new Option<DistributionSpec?>(
                    "--distribution",
                    description: "The distribution to use if targeting a BuildConfig.json file.",
                    parseArgument: DistributionSpec.ParseDistributionSpec(serviceProvider, Path),
                    isDefault: true);
                Distribution.AddAlias("-d");
                Distribution.Arity = ArgumentArity.ZeroOrOne;

                SupplyProject = new Option<bool>(
                    "--project",
                    description: "If set, supplies the '-project' argument as the last argument to UAT. The project path is not automatically set for UAT, because it is only allowed if the sub-command for UAT wants the project file path.");
                SupplyProject.Arity = ArgumentArity.ZeroOrOne;

                Target = new Argument<string>(
                    "target",
                    description: "The target to run.");
                Target.FromAmong([
                    "editor",
                    "editor-cmd",
                    "game",
                    "uat",
                    "ubt",
                    "uba-visualiser",
                    "uba-visualizer",
                ]);
                Target.Arity = ArgumentArity.ExactlyOne;
                Target.HelpName = "target";
                Target.SetDefaultValue("editor");

                Arguments = new Argument<string[]>(
                    "arguments",
                    description: "All remaining arguments are passed to the target as-is.");
                Arguments.Arity = ArgumentArity.ZeroOrMore;
            }
        }

        public static Command CreateRunCommand()
        {
            var command = new Command("run", "Run the editor, game or related tool such as UAT or UBT.")
            {
                FullDescription = """
                This command runs the editor, game or related tool such as UAT or UBT. This is primarily useful so you don't have to remember the exact path to tools such as UAT.

                The 'target' argument specifies what to run, and it can be any of the following:

                editor:         Run the Unreal Engine editor.
                editor-cmd:     Run the Unreal Engine editor as a command-line application.
                game:           Run the Unreal Engine game for this project on the current platform. This uses the editor binary to quickly launch the game. This can only be used in a project context.
                uat:            Run UnrealAutomationTool.
                ubt:            Run UnrealBuildTool.
                uba-visualizer: Run the Unreal Build Accelerator visualizer.
 
                If --path points to a project file, the target will automatically receive the project file as an argument in an appropriate manner, if possible.
                """
            };
            command.AddServicedOptionsHandler<RunCommandInstance, Options>();
            return command;
        }

        private sealed class RunCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunCommandInstance> _logger;
            private readonly Options _options;
            private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
            private readonly IProcessExecutor _processExecutor;

            public RunCommandInstance(
                ILogger<RunCommandInstance> logger,
                Options options,
                IEngineWorkspaceProvider engineWorkspaceProvider,
                IProcessExecutor processExecutor)
            {
                _logger = logger;
                _options = options;
                _engineWorkspaceProvider = engineWorkspaceProvider;
                _processExecutor = processExecutor;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                try
                {
                    var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                    var path = context.ParseResult.GetValueForOption(_options.Path);
                    var distribution = context.ParseResult.GetValueForOption(_options.Distribution);
                    var supplyProject = context.ParseResult.GetValueForOption(_options.SupplyProject)!;
                    var target = context.ParseResult.GetValueForArgument(_options.Target)!.ToLowerInvariant();
                    var arguments = context.ParseResult.GetValueForArgument(_options.Arguments)!;

                    var engineSpec = engine.ToBuildEngineSpecification("keep-wireless-enabled");

                    var configurationPreferences = new[]
                    {
                        "Debug",
                        "DebugGame",
                        "Development"
                    };

                    var platformName = true switch
                    {
                        var _ when OperatingSystem.IsWindows() => "Win64",
                        var _ when OperatingSystem.IsMacOS() => "Mac",
                        var _ when OperatingSystem.IsLinux() => "Linux",
                        _ => throw new PlatformNotSupportedException(),
                    };

                    string? projectPath = null;
                    var editorTargetName = "UnrealEditor";
                    if (path != null && path.Type == PathSpecType.UProject)
                    {
                        projectPath = path.UProjectPath;
                        var sourceFolder = Path.Combine(path.DirectoryPath, "Source");
                        if (Directory.Exists(sourceFolder))
                        {
                            foreach (var editorFile in Directory.GetFiles(sourceFolder, "*Editor.Target.cs"))
                            {
                                editorTargetName = Path.GetFileName(editorFile);
                                editorTargetName = editorTargetName.Substring(0, editorTargetName.IndexOf('.', StringComparison.Ordinal));
                                _logger.LogInformation($"Detected editor target name from files in Source directory: {editorTargetName}");
                                break;
                            }
                        }
                    }
                    else if (
                        path != null && path.Type == PathSpecType.BuildConfig &&
                        distribution != null && distribution.Distribution is BuildConfigProjectDistribution projectDistribution)
                    {
                        projectPath = Path.Combine(
                            path.DirectoryPath,
                            projectDistribution.FolderName,
                            $"{projectDistribution.FolderName}.uproject");
                        editorTargetName = projectDistribution.Build.Editor?.Target ?? "UnrealEditor";
                    }

                    void LogExecution(string filePath, List<LogicalProcessArgument> arguments, bool isOnlyStarting)
                    {
                        _logger.LogInformation($"{(isOnlyStarting ? "Starting" : "Running")}: '{filePath}' {string.Join(" ", arguments.Select(x => $"'{x}'"))}");
                    }

                    await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                        engineSpec,
                        string.Empty,
                        context.GetCancellationToken()).ConfigureAwait(false))
                            .AsAsyncDisposable(out var engineWorkspace)
                            .ConfigureAwait(false))
                    {
                        switch (target)
                        {
                            case "editor":
                            case "editor-cmd":
                            case "game":
                                {
                                    var cmdSuffix = target == "editor-cmd" ? "-Cmd" : string.Empty;
                                    var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;

                                    List<(string editorPath, DateTimeOffset lastModulesModificationTime)> moduleAndEngineTargets = [];
                                    var attemptedPaths = new List<string>();
                                    void SearchForPaths()
                                    {
                                        moduleAndEngineTargets.Clear();
                                        attemptedPaths.Clear();
                                        foreach (var configuration in configurationPreferences)
                                        {
                                            string modulePath;
                                            string editorPath;
                                            if (configuration == "Development")
                                            {
                                                modulePath = Path.Combine(
                                                    Path.GetDirectoryName(projectPath)!,
                                                    "Binaries",
                                                    platformName,
                                                    "UnrealEditor.modules");
                                                editorPath = Path.Combine(
                                                    engineWorkspace.Path,
                                                    "Engine",
                                                    "Binaries",
                                                    platformName,
                                                    $"UnrealEditor{cmdSuffix}{executableSuffix}");
                                            }
                                            else
                                            {
                                                modulePath = Path.Combine(
                                                    Path.GetDirectoryName(projectPath)!,
                                                    "Binaries",
                                                    platformName,
                                                    $"UnrealEditor-{platformName}-{configuration}.modules");
                                                editorPath = Path.Combine(
                                                    engineWorkspace.Path,
                                                    "Engine",
                                                    "Binaries",
                                                    platformName,
                                                    $"UnrealEditor-{platformName}-{configuration}{cmdSuffix}{executableSuffix}");
                                            }
                                            attemptedPaths.Add(modulePath);
                                            if (File.Exists(modulePath) && File.Exists(editorPath))
                                            {
                                                moduleAndEngineTargets.Add((editorPath, File.GetLastWriteTimeUtc(modulePath)));
                                            }
                                        }
                                    }
                                    SearchForPaths();

                                    if (moduleAndEngineTargets.Count == 0)
                                    {
                                        // The editor isn't built; try to build it.
                                        _logger.LogWarning("This project hasn't been built for any configurations, or the editor binaries don't exist as expected. The modules that were searched for were:");
                                        foreach (var attemptedPath in attemptedPaths)
                                        {
                                            _logger.LogWarning($"  {attemptedPath}");
                                        }
                                        _logger.LogWarning("Attempting to build the editor on-demand...");

                                        var scriptSuffix = OperatingSystem.IsWindows() ? ".bat" : ".sh";
                                        var ubtPath = Path.Combine(engineWorkspace.Path, "Engine", "Build", "BatchFiles", $"RunUBT{scriptSuffix}");
                                        var buildPath = Path.Combine(engineWorkspace.Path, "Engine", "Build", "BatchFiles", $"Build{scriptSuffix}");
                                        if (!File.Exists(ubtPath) && File.Exists(buildPath))
                                        {
                                            ubtPath = buildPath;
                                        }
                                        if (!File.Exists(ubtPath))
                                        {
                                            _logger.LogError($"Unable to locate the build script that was expected to exist at: {ubtPath}");
                                            return 1;
                                        }

                                        var buildArguments = new List<LogicalProcessArgument>
                                        {
                                            editorTargetName,
                                            platformName,
                                            "Development"
                                        };
                                        if (projectPath != null)
                                        {
                                            buildArguments.Add($"-project=\"{projectPath}\"");
                                        }

                                        var buildExitCode = await _processExecutor.ExecuteAsync(
                                            new ProcessSpecification
                                            {
                                                FilePath = ubtPath,
                                                Arguments = buildArguments,
                                                WorkingDirectory = engineWorkspace.Path,
                                            },
                                            CaptureSpecification.Passthrough,
                                            context.GetCancellationToken()).ConfigureAwait(false);
                                        if (buildExitCode != 0)
                                        {
                                            _logger.LogError($"RunUBT exited with non-zero exit code {buildExitCode}.");
                                            return buildExitCode;
                                        }

                                        // Try to find the editor again.
                                        SearchForPaths();
                                        if (moduleAndEngineTargets.Count == 0)
                                        {
                                            _logger.LogError("Still can't find the editor after successfully building it. UET probably needs to be updated to handle whatever path it got built to!");
                                            return 1;
                                        }
                                    }

                                    var foundPath = moduleAndEngineTargets
                                        .OrderByDescending(x => x.lastModulesModificationTime)
                                        .Select(x => x.editorPath)
                                        .First();

                                    if (moduleAndEngineTargets.Count > 1)
                                    {
                                        _logger.LogInformation("Multiple built configurations of the project were found. The configuration with the newest build will be selected:");
                                        foreach (var entry in moduleAndEngineTargets.OrderByDescending(x => x.lastModulesModificationTime))
                                        {
                                            _logger.LogInformation($" - {entry.lastModulesModificationTime}: {entry.editorPath}");
                                        }
                                    }

                                    var runArguments = new List<LogicalProcessArgument>();
                                    if (projectPath != null)
                                    {
                                        runArguments.Add($"-project=\"{projectPath}\"");
                                    }
                                    if (target == "game")
                                    {
                                        runArguments.Add($"-game");
                                    }
                                    runArguments.AddRange(arguments.Select(x => new LogicalProcessArgument(x)));

                                    if (target == "editor-cmd")
                                    {
                                        LogExecution(foundPath, runArguments, false);
                                        return await _processExecutor.ExecuteAsync(
                                            new ProcessSpecification
                                            {
                                                FilePath = foundPath,
                                                Arguments = runArguments,
                                                WorkingDirectory = engineWorkspace.Path,
                                            },
                                            CaptureSpecification.Passthrough,
                                            context.GetCancellationToken()).ConfigureAwait(false);
                                    }
                                    else
                                    {
                                        // For 'editor' and 'game' targets, use Process.Start so we can "fire and forget". This ensures
                                        // the editor or game doesn't close when the user hits Ctrl-C on UET.
                                        LogExecution(foundPath, runArguments, true);
                                        var startInfo = new ProcessStartInfo
                                        {
                                            FileName = foundPath,
                                            WorkingDirectory = engineWorkspace.Path,
                                        };
                                        foreach (var arg in runArguments)
                                        {
                                            startInfo.ArgumentList.Add(arg.LogicalValue);
                                        }
                                        _ = Process.Start(startInfo);
                                        return 0;
                                    }
                                }
                            case "uat":
                            case "ubt":
                                {
                                    var scriptSuffix = OperatingSystem.IsWindows() ? ".bat" : ".sh";
                                    var toolName = target == "uat" ? "RunUAT" : "RunUBT";
                                    var toolPath = Path.Combine(engineWorkspace.Path, "Engine", "Build", "BatchFiles", $"{toolName}{scriptSuffix}");
                                    var altToolPath = Path.Combine(engineWorkspace.Path, "Engine", "Build", "BatchFiles", $"Build{scriptSuffix}");
                                    if (toolName == "RunUBT" && !File.Exists(toolPath) && File.Exists(altToolPath))
                                    {
                                        toolPath = altToolPath;
                                    }
                                    if (!File.Exists(toolPath))
                                    {
                                        _logger.LogError($"Unable to locate the tool launching script that was expected to exist at: {toolPath}");
                                        return 1;
                                    }

                                    var toolArguments = new List<LogicalProcessArgument>();
                                    if (projectPath != null)
                                    {
                                        if (target == "uat")
                                        {
                                            toolArguments.Add($"-ScriptsForProject=\"{projectPath}\"");
                                        }
                                        else
                                        {
                                            toolArguments.Add($"-project=\"{projectPath}\"");
                                        }
                                    }
                                    toolArguments.AddRange(arguments.Select(x => new LogicalProcessArgument(x)));
                                    if (projectPath != null && target == "uat" && supplyProject)
                                    {
                                        toolArguments.Add($"-project=\"{projectPath}\"");
                                    }

                                    LogExecution(toolPath, toolArguments, false);
                                    var runExitCode = await _processExecutor.ExecuteAsync(
                                        new ProcessSpecification
                                        {
                                            FilePath = toolPath,
                                            Arguments = toolArguments,
                                            WorkingDirectory = engineWorkspace.Path,
                                        },
                                        CaptureSpecification.Passthrough,
                                        context.GetCancellationToken()).ConfigureAwait(false);

                                    return runExitCode;
                                }
                            case "uba-visualiser":
                            case "uba-visualizer":
                                {
                                    var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
                                    var toolPath = Path.Combine(
                                        engineWorkspace.Path,
                                        "Engine",
                                        "Binaries",
                                        platformName,
                                        "UnrealBuildAccelerator",
                                        RuntimeInformation.OSArchitecture == Architecture.X64 ? "x64" : "arm64",
                                        $"UbaVisualizer{executableSuffix}");
                                    if (!File.Exists(toolPath))
                                    {
                                        _logger.LogError($"The path '{toolPath}' does not exist. UET can not build the UBA visualiser on-demand yet.");
                                        return 1;
                                    }

                                    LogExecution(toolPath, [], false);
                                    var runExitCode = await _processExecutor.ExecuteAsync(
                                        new ProcessSpecification
                                        {
                                            FilePath = toolPath,
                                            Arguments = [],
                                            WorkingDirectory = engineWorkspace.Path,
                                        },
                                        CaptureSpecification.Passthrough,
                                        context.GetCancellationToken()).ConfigureAwait(false);
                                    return runExitCode;
                                }
                            default:
                                _logger.LogError($"The target '{target}' is not supported.");
                                return 1;
                        }
                    }
                }
                catch (OperationCanceledException) when (context.GetCancellationToken().IsCancellationRequested)
                {
                    // Expected when the user hits Ctrl-C.
                    return 0;
                }
            }
        }
    }
}
