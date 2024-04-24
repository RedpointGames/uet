namespace UET.Commands.Generate
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;

    internal static class GenerateCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec[]> Path;
            public Option<DirectoryInfo[]> AutomationPath;
            public Option<bool> Open;

            public Options()
            {
                Path = new Option<PathSpec[]>(
                    "--path",
                    description: "The directory path that contains a .uproject file.",
                    parseArgument: result => PathSpec.ParsePathSpecs(result, true),
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.OneOrMore;

                AutomationPath = new Option<DirectoryInfo[]>(
                    "--automation-path",
                    description: "When generating for an engine, additional folders to that contain automation projects.");
                AutomationPath.AddAlias("-a");
                AutomationPath.Arity = ArgumentArity.ZeroOrMore;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to target the generated project files at.",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, null),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ZeroOrOne;

                Open = new Option<bool>(
                    "--open",
                    description: "Open the generated project files in the default editor (Visual Studio or Xcode) after they've been generated.");
                Open.AddAlias("-o");
            }
        }

        public static ICommandLineBuilder RegisterGenerateCommand(this ICommandLineBuilder rootBuilder)
        {
            rootBuilder.AddCommand<GenerateCommandInstance, Options>(
                _ =>
                {
                    return new Command("generate", "Generate Visual Studio or Xcode project files for Unreal Engine or an Unreal Engine project.")
                    {
                        FullDescription = """
                        This command generates project files for either Unreal Engine or an Unreal Engine project. The behaviour of this command depends on the arguments that you've passed to it and the current directory.
                
                        After the command runs, it will tell you where the project files were generated. You can also use --open to open the project files after they've been generated, without having to start Visual Studio or Xcode manually.
                
                        -------------

                        This command generates project files for a specific Unreal Engine project in the following scenarios:
                
                        - If there is only a single project file specified, or a single project is inferred from the current directory, and
                        - The current directory does not contain Unreal Engine itself (./Engine/Build/Build.version must not exist).
                
                        When this command is generating projects for a specific Unreal Engine project, the project files are generated inside the project's folder (such as "ProjectName.sln" for Visual Studio). This solution will include the Unreal Engine project and build tools such as UBT, but will not contain any Unreal Engine C++ tooling (such as the ShaderCompilerWorker).
                
                        -------------

                        This command generates project files for Unreal Engine itself in the following scenarios:

                        - If the current directory contains Unreal Engine itself (./Engine/Build/Build.version exists), or
                        - If multiple project files are specified on the command line with --path|-p, and the engine path is specified by --engine|-e.
                        - In both cases, the referenced Unreal Engine must be a source engine (not an installed build). Passing multiple project files is an error if the engine is an installed engine (e.g. installed from the Epic Games launcher).
                
                        When this command is generating projects for Unreal Engine, the project files are generated inside the Unreal Engine folder (such as "UE5.sln" for Visual Studio). This solution will contain all of the engine programs, build tools and optionally any additional Unreal Engine projects you specified on the command line.
                        """
                    };
                });
            return rootBuilder;
        }

        private sealed class GenerateCommandInstance : ICommandInstance
        {
            private readonly ILogger<GenerateCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public GenerateCommandInstance(
                ILogger<GenerateCommandInstance> logger,
                IProcessExecutor processExecutor,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _options = options;
            }

            private static IEnumerable<DirectoryInfo> DiscoverAutomationProjectDirectories(IEnumerable<DirectoryInfo> rootPaths)
            {
                foreach (var rootPath in rootPaths)
                {
                    foreach (var automationProjectDirectory in DiscoverAutomationProjectDirectories(rootPath))
                    {
                        yield return automationProjectDirectory;
                    }
                }
            }

            private static IEnumerable<DirectoryInfo> DiscoverAutomationProjectDirectories(DirectoryInfo rootPath)
            {
                var automationProject = rootPath.GetFiles("*.Automation.csproj");
                if (automationProject.Length > 0)
                {
                    yield return rootPath;
                    yield break;
                }

                foreach (var subdirectory in rootPath.GetDirectories())
                {
                    foreach (var automationProjectDirectory in DiscoverAutomationProjectDirectories(subdirectory))
                    {
                        yield return automationProjectDirectory;
                    }
                }
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var paths = context.ParseResult.GetValueForOption(_options.Path) ?? Array.Empty<PathSpec>();
                var open = context.ParseResult.GetValueForOption(_options.Open);
                var automationPaths = context.ParseResult.GetValueForOption(_options.AutomationPath) ?? Array.Empty<DirectoryInfo>();

                // Compute how to invoke project generation.
                string workingDirectory;
                string outputFolder;
                string outputFilenameWithoutExtension;
                string scriptName;
                var arguments = new List<string>();
                if ((engine.Type == EngineSpecType.Path &&
                     string.Equals(engine.Path, Environment.CurrentDirectory, StringComparison.OrdinalIgnoreCase) &&
                     !File.Exists(Path.Combine(engine.Path!, "Engine", "Build", "InstalledBuild.txt"))) ||
                     paths.Length != 1 ||
                     ((context.ParseResult.FindResultFor(_options.Path)?.Tokens?.Count ?? 0) == 0 &&
                      paths.First().Type != PathSpecType.UProject))
                {
                    // We want to generate project files for an engine.
                    var installedBuild = Path.Combine(engine.Path!, "Engine", "Build", "InstalledBuild.txt");
                    if (File.Exists(installedBuild))
                    {
                        _logger.LogError("This command needed to generate project files at the engine level because multiple projects were specified with --path|-p. However, the targeted engine is an installed build (not a source engine), so project files can't be generated. Target a source version of Unreal Engine or specify only a single project.");
                        return 1;
                    }

                    _logger.LogInformation($"Generating project files for Unreal Engine located at: {engine.Path}");
                    workingDirectory = engine.Path!;
                    scriptName = "GenerateProjectFiles";
                    arguments.Add("-rocket");
                    if (paths.Length > 1)
                    {
                        var uprojectDirs = Path.Combine(engine.Path!, ".uprojectdirs");
                        if (File.Exists(uprojectDirs))
                        {
                            var existingLines = File.ReadAllLines(uprojectDirs);
                            if (existingLines.Length > 0 && !existingLines.Any(x => x.StartsWith("; Managed by UET", StringComparison.OrdinalIgnoreCase)))
                            {
                                _logger.LogError($"This engine already contains a .uprojectdirs file, which UET must set in order to incorporate multiple Unreal Engine projects into the generated project files. However, the .uprojectdirs file does not appear to be managed by UET, and UET will not overwrite an existing .uprojectdirs value in case it has been intentionally configured by the user. Remove the .uprojectdirs file from '{uprojectDirs}', specify a single project file on the command line, or specify no project file at all.");
                                return 1;
                            }
                        }
                        var projectLines = new List<string>
                        {
                            "; Managed by UET",
                        };
                        var wantedHoldingDirectory = false;
                        var wantedAutomationToolDirectory = false;
                        // @note: We intentionally use a tiny folder name here to reduce path lengths.
                        var uetManagedProjectsDirectory = Path.Combine(engine.Path!, "P");
                        var uetManagedAutomationToolProjectsDirectory = Path.Combine(engine.Path!, "P", "AutomationTool");
                        foreach (var path in paths)
                        {
                            if (path!.Type != PathSpecType.UProject)
                            {
                                _logger.LogWarning($"Ignoring the path '{path.DirectoryPath}' as it does not refer to a .uproject file. It will not be included in the generated project files for the engine.");
                            }
                            if (path.UProjectPath!.StartsWith(engine.Path!, StringComparison.OrdinalIgnoreCase))
                            {
                                // This can be directly included in .uprojectdirs.
                                projectLines.Add(Path.GetDirectoryName(path.UProjectPath)!);
                            }
                            else
                            {
                                // Otherwise, we need to do junction/symlink magic so that UBT thinks
                                // the project sits under the engine.
                                if (!wantedHoldingDirectory)
                                {
                                    Directory.CreateDirectory(uetManagedProjectsDirectory);
                                    wantedHoldingDirectory = true;
                                }
                                if (Directory.Exists(Path.Combine(uetManagedProjectsDirectory, Path.GetFileNameWithoutExtension(path.UProjectPath)!)))
                                {
                                    Directory.Delete(Path.Combine(uetManagedProjectsDirectory, Path.GetFileNameWithoutExtension(path.UProjectPath)!));
                                }
                                Directory.CreateSymbolicLink(
                                    Path.Combine(uetManagedProjectsDirectory, Path.GetFileNameWithoutExtension(path.UProjectPath)!),
                                    Path.GetDirectoryName(path.UProjectPath)!);
                            }
                        }
                        foreach (var automationPath in DiscoverAutomationProjectDirectories(automationPaths))
                        {
                            if (!wantedAutomationToolDirectory)
                            {
                                Directory.CreateDirectory(uetManagedAutomationToolProjectsDirectory);
                                wantedAutomationToolDirectory = true;
                            }
                            if (Directory.Exists(Path.Combine(uetManagedAutomationToolProjectsDirectory, automationPath.Name)))
                            {
                                Directory.Delete(Path.Combine(uetManagedAutomationToolProjectsDirectory, automationPath.Name));
                            }
                            Directory.CreateSymbolicLink(
                                Path.Combine(uetManagedAutomationToolProjectsDirectory, automationPath.Name),
                                automationPath.FullName);
                        }
                        if (wantedAutomationToolDirectory)
                        {
                            projectLines.Add(uetManagedAutomationToolProjectsDirectory);
                        }
                        if (wantedHoldingDirectory)
                        {
                            projectLines.Add(uetManagedProjectsDirectory);
                        }
                        File.WriteAllLines(
                            uprojectDirs,
                            projectLines);
                    }
                    else
                    {
                        arguments.Add($"-project={paths[0].UProjectPath}");
                    }
                    outputFolder = engine.Path!;
                    outputFilenameWithoutExtension = "UE5";
                }
                else
                {
                    // We want to generate project files for a specific project.
                    var path = paths.First();
                    if (path.Type != PathSpecType.UProject)
                    {
                        _logger.LogError($"The path '{path.DirectoryPath}' does not refer to a .uproject file. Specify the directory that contains a .uproject file with --path|-p or if you want to generate project files for the engine itself, specify the path to the engine with --engine|-e.");
                        return 1;
                    }
                    if (engine.Path == null)
                    {
                        _logger.LogError($"The engine path could not be inferred from the project file. Specify the engine to use with --engine|-e.");
                        return 1;
                    }

                    _logger.LogInformation($"Generating project files for the Unreal Engine .uproject located at: {path.UProjectPath}");
                    workingDirectory = Path.GetDirectoryName(path.UProjectPath)!;
                    arguments.AddRange(new[]
                    {
                        "-projectfiles",
                        $"-project={path.UProjectPath}",
                        "-rocket",
                    });
                    outputFolder = Path.GetDirectoryName(path.UProjectPath)!;
                    outputFilenameWithoutExtension = Path.GetFileNameWithoutExtension(path.UProjectPath)!;
                    scriptName = "Build";
                }

                // Run project generation.
                var processSpecification = new ProcessSpecification
                {
                    FilePath = true switch
                    {
                        var v when v == OperatingSystem.IsWindows() => Path.Combine(engine.Path!, "Engine", "Build", "BatchFiles", $"{scriptName}.bat"),
                        var v when v == OperatingSystem.IsMacOS() => Path.Combine(engine.Path!, "Engine", "Build", "BatchFiles", "Mac", $"{scriptName}.sh"),
                        var v when v == OperatingSystem.IsLinux() => Path.Combine(engine.Path!, "Engine", "Build", "BatchFiles", "Linux", $"{scriptName}.sh"),
                        _ => throw new PlatformNotSupportedException(),
                    },
                    Arguments = arguments.ToArray(),
                    WorkingDirectory = workingDirectory,
                };
                var exitCode = await _processExecutor.ExecuteAsync(
                    processSpecification,
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken()).ConfigureAwait(false);

                if (exitCode == 0)
                {
                    _logger.LogInformation($"Project generation finished successfully. The generated project files are located in: {outputFolder}");
                    if (open)
                    {
                        if (OperatingSystem.IsWindows())
                        {
                            var solutionPath = Path.Combine(workingDirectory, outputFilenameWithoutExtension + ".sln");
                            if (File.Exists(solutionPath))
                            {
                                _logger.LogInformation($"Opening generated project file due to --open|-o being passed: {solutionPath}");
                                Process.Start(new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    FileName = solutionPath,
                                });
                            }
                            else
                            {
                                _logger.LogWarning("Unable to automatically open generated project file because it doesn't exist. This can happen if you've configured Unreal Engine to generate project files for something other than Visual Studio.");
                            }
                        }
                        else if (OperatingSystem.IsMacOS())
                        {
                            var solutionPath = Path.Combine(workingDirectory, outputFilenameWithoutExtension + ".xcworkspace");
                            if (File.Exists(solutionPath))
                            {
                                _logger.LogInformation($"Opening generated project file due to --open|-o being passed: {solutionPath}");
                                Process.Start(new ProcessStartInfo
                                {
                                    UseShellExecute = true,
                                    FileName = solutionPath,
                                });
                            }
                            else
                            {
                                _logger.LogWarning("Unable to automatically open generated project file because it doesn't exist. This can happen if you've configured Unreal Engine to generate project files for something other than Xcode.");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Opening generated project files with --open|-o is not supported on this platform.");
                        }
                    }
                }
                else
                {
                    _logger.LogError($"Project generation failed. See above for details.");
                }
                return exitCode;
            }
        }
    }
}
