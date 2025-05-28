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
    using Redpoint.Uet.SdkManagement;

    internal sealed class RunCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<DistributionSpec?> Distribution;
            public Option<bool> SupplyProject;
            public Option<bool> ForceBuild;
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

                ForceBuild = new Option<bool>(
                    "--force-build",
                    description: "If set, always builds the relevant editor target before launching, even if UET considers the target to be up-to-date enough for a launch to happen. If you're getting 'Failed to build. Please retry building through your IDE.', this option can be used to get the build logs appearing in the terminal prior to the launch attempt.");
                ForceBuild.Arity = ArgumentArity.ZeroOrOne;

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
                    "adb",
                    "xcode",
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
                adb:            Run the Android Debug Bridge.
                xcode:          Run the version of Xcode that this Unreal Engine version requires.
 
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
            private readonly ILocalSdkManager _localSdkManager;
            private readonly IServiceProvider _serviceProvider;

            public RunCommandInstance(
                ILogger<RunCommandInstance> logger,
                Options options,
                IEngineWorkspaceProvider engineWorkspaceProvider,
                IProcessExecutor processExecutor,
                ILocalSdkManager localSdkManager,
                IServiceProvider serviceProvider)
            {
                _logger = logger;
                _options = options;
                _engineWorkspaceProvider = engineWorkspaceProvider;
                _processExecutor = processExecutor;
                _localSdkManager = localSdkManager;
                _serviceProvider = serviceProvider;
            }

            private static string ParameterisedArgument(string argumentName, string argumentValue)
            {
                if (OperatingSystem.IsWindows())
                {
                    return $"-{argumentName}=\"{argumentValue}\"";
                }
                else
                {
                    return $"-{argumentName}={argumentValue}";
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                try
                {
                    var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                    var path = context.ParseResult.GetValueForOption(_options.Path);
                    var distribution = context.ParseResult.GetValueForOption(_options.Distribution);
                    var supplyProject = context.ParseResult.GetValueForOption(_options.SupplyProject)!;
                    var forceBuild = context.ParseResult.GetValueForOption(_options.ForceBuild)!;
                    var target = context.ParseResult.GetValueForArgument(_options.Target)!.ToLowerInvariant();
                    var arguments = context.ParseResult.GetValueForArgument(_options.Arguments)!;

                    var engineSpec = engine.ToBuildEngineSpecification("run");

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
                        IDictionary<string, string>? hostEnvVars = null;
                        if (OperatingSystem.IsMacOS() && target != "adb")
                        {
                            // We need to grab SDK environment variables on macOS because UET always resets the DEVELOPER_DIR environment
                            // variable, and this interferes with the editor starting up (as it requires an actual Xcode install).
                            var packagePath = UetPaths.UetDefaultMacSdkStoragePath;
                            Directory.CreateDirectory(packagePath);
                            hostEnvVars = await _localSdkManager.SetupEnvironmentForSdkSetups(
                                engineWorkspace.Path,
                                packagePath,
                                _serviceProvider.GetServices<ISdkSetup>()
                                    .Where(x => x.PlatformNames.Contains("Mac"))
                                    .ToHashSet(),
                                context.GetCancellationToken()).ConfigureAwait(false);
                            if (hostEnvVars != null)
                            {
                                foreach (var kv in hostEnvVars)
                                {
                                    _logger.LogInformation($"Setting environment variable from SDK: {kv.Key}={kv.Value}");
                                    Environment.SetEnvironmentVariable(kv.Key, kv.Value);
                                }
                            }
                        }

                        string GetUbtPath()
                        {
                            var scriptSuffix = OperatingSystem.IsWindows() ? ".bat" : ".sh";
                            var ubtPath = Path.Combine(engineWorkspace.Path, "Engine", "Build", "BatchFiles", $"RunUBT{scriptSuffix}");
                            var buildPath = Path.Combine(engineWorkspace.Path, "Engine", "Build", "BatchFiles", $"Build{scriptSuffix}");
                            if (!File.Exists(ubtPath) && File.Exists(buildPath))
                            {
                                ubtPath = buildPath;
                            }
                            return ubtPath;
                        }

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
                                            if (projectPath != null)
                                            {
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
                                            }
                                            else
                                            {
                                                if (configuration == "Development")
                                                {
                                                    modulePath = Path.Combine(
                                                        engineWorkspace.Path,
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
                                                        engineWorkspace.Path,
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
                                            }
                                            attemptedPaths.Add(modulePath);
                                            if (File.Exists(modulePath) && File.Exists(editorPath))
                                            {
                                                moduleAndEngineTargets.Add((editorPath, File.GetLastWriteTimeUtc(modulePath)));
                                            }
                                        }
                                    }

                                    if (!forceBuild)
                                    {
                                        SearchForPaths();
                                    }

                                    var targetListToBuild = new List<string>();

                                    string[] engineToolsToBuild = [
                                        "ShaderCompileWorker",
                                        "UnrealPak",
                                    ];
                                    foreach (var engineTool in engineToolsToBuild)
                                    {
                                        if (!File.Exists(Path.Combine(
                                            engineWorkspace.Path,
                                            "Binaries",
                                            platformName,
                                            $"{engineTool}.modules")))
                                        {
                                            targetListToBuild.Add($"{engineTool} Development {platformName}");
                                        }
                                    }

                                    if (moduleAndEngineTargets.Count == 0)
                                    {
                                        if (projectPath != null)
                                        {
                                            targetListToBuild.Add($"{ParameterisedArgument("project", projectPath)} {editorTargetName} Development {platformName}");
                                        }
                                        else
                                        {
                                            targetListToBuild.Add($"{editorTargetName} Development {platformName}");
                                        }
                                    }

                                    if (targetListToBuild.Count > 0)
                                    {
                                        // The editor isn't built; try to build it.
                                        if (!forceBuild)
                                        {
                                            _logger.LogWarning("This project hasn't been built for any configurations, or the editor binaries don't exist as expected. The modules that were searched for were:");
                                            foreach (var attemptedPath in attemptedPaths)
                                            {
                                                _logger.LogWarning($"  {attemptedPath}");
                                            }
                                        }

                                        _logger.LogWarning("Attempting to build the editor on-demand...");

                                        var ubtPath = GetUbtPath();
                                        if (!File.Exists(ubtPath))
                                        {
                                            _logger.LogError($"Unable to locate the build script that was expected to exist at: {ubtPath}");
                                            return 1;
                                        }

                                        var targetListPath = Path.GetTempFileName();
                                        await File.WriteAllLinesAsync(targetListPath, targetListToBuild, context.GetCancellationToken());

                                        var buildArguments = new List<LogicalProcessArgument>
                                        {
                                            ParameterisedArgument("TargetList", targetListPath),
                                        };

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
                                        runArguments.Add(ParameterisedArgument("project", projectPath));
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
                                            toolArguments.Add(ParameterisedArgument("ScriptsForProject", projectPath));
                                        }
                                        else
                                        {
                                            toolArguments.Add(ParameterisedArgument("project", projectPath));
                                        }
                                    }
                                    toolArguments.AddRange(arguments.Select(x => new LogicalProcessArgument(x)));
                                    if (projectPath != null && target == "uat" && supplyProject)
                                    {
                                        toolArguments.Add(ParameterisedArgument("project", projectPath));
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
                                    var ubaRootPath = Path.Combine(
                                        engineWorkspace.Path,
                                        "Engine",
                                        "Binaries",
                                        platformName,
                                        "UnrealBuildAccelerator",
                                        RuntimeInformation.OSArchitecture == Architecture.X64 ? "x64" : "arm64");

                                    var targetListToBuild = new List<string>();

                                    string[] engineToolsToBuild = [
                                        "UbaAgent",
                                        "UbaDetours",
                                        "UbaHost",
                                        "UbaObjTool",
                                        "UbaVisualizer",
                                    ];
                                    foreach (var engineTool in engineToolsToBuild)
                                    {
                                        if (!File.Exists(Path.Combine(ubaRootPath, $"{engineTool}{executableSuffix}")) &&
                                            !File.Exists(Path.Combine(ubaRootPath, $"{engineTool}.dll")) &&
                                            !File.Exists(Path.Combine(ubaRootPath, $"{engineTool}.dylib")) &&
                                            !File.Exists(Path.Combine(ubaRootPath, $"{engineTool}.so")))
                                        {
                                            targetListToBuild.Add($"{engineTool} Development {platformName}");
                                        }
                                    }

                                    if (targetListToBuild.Count > 0)
                                    {
                                        _logger.LogWarning("Attempting to build UBA on-demand...");

                                        var ubtPath = GetUbtPath();
                                        if (!File.Exists(ubtPath))
                                        {
                                            _logger.LogError($"Unable to locate the build script that was expected to exist at: {ubtPath}");
                                            return 1;
                                        }

                                        var targetListPath = Path.GetTempFileName();
                                        await File.WriteAllLinesAsync(targetListPath, targetListToBuild, context.GetCancellationToken());

                                        var buildArguments = new List<LogicalProcessArgument>
                                        {
                                            ParameterisedArgument("TargetList", targetListPath),
                                        };
                                        if (OperatingSystem.IsWindows())
                                        {
                                            buildArguments.Add(ParameterisedArgument("Compiler", "VisualStudio2022"));
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
                                    }

                                    var toolPath = Path.Combine(ubaRootPath, $"UbaVisualizer{executableSuffix}");
                                    if (!File.Exists(toolPath))
                                    {
                                        _logger.LogError($"The path '{toolPath}' does not exist. UET should have built UBA on-demand if necessary.");
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
                            case "adb":
                                {
                                    var packagePath = OperatingSystem.IsWindows() ? UetPaths.UetDefaultWindowsSdkStoragePath : UetPaths.UetDefaultMacSdkStoragePath;
                                    Directory.CreateDirectory(packagePath);
                                    var androidEnvVars = await _localSdkManager.SetupEnvironmentForSdkSetups(
                                        engineWorkspace.Path,
                                        packagePath,
                                        _serviceProvider.GetServices<ISdkSetup>()
                                            .Where(x => x.PlatformNames.Contains("Android"))
                                            .ToHashSet(),
                                        context.GetCancellationToken()).ConfigureAwait(false);

                                    var executableSuffix = OperatingSystem.IsWindows() ? ".exe" : string.Empty;
                                    var toolPath = Path.Combine(
                                        androidEnvVars["ANDROID_HOME"],
                                        "platform-tools",
                                        $"adb{executableSuffix}");
                                    if (!File.Exists(toolPath))
                                    {
                                        _logger.LogError($"The path '{toolPath}' does not exist. UET can not run ADB.");
                                        return 1;
                                    }

                                    var toolArguments = arguments
                                        .Select(x => new LogicalProcessArgument(x))
                                        .ToList();
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
                            case "xcode":
                                {
                                    if (!OperatingSystem.IsMacOS())
                                    {
                                        _logger.LogError("You can't run Xcode on anything other than macOS!");
                                        return 1;
                                    }

                                    var developerDir = hostEnvVars!["DEVELOPER_DIR"];
                                    var xcodeBinary = Path.Combine(developerDir, "Contents", "MacOS", "Xcode");

                                    LogExecution(xcodeBinary, [], true);
                                    var startInfo = new ProcessStartInfo
                                    {
                                        FileName = xcodeBinary,
                                    };
                                    _ = Process.Start(startInfo);
                                    return 0;
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
