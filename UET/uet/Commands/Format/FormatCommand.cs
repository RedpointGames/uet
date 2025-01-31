namespace UET.Commands.Format
{
    using B2Net.Models;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UET.BuildConfig;
    using UET.Commands.EngineSpec;

    internal sealed class FormatCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;
            public Option<bool> DryRun;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file, a .uplugin file, or a BuildConfig.json file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use to detect the relevant compiler toolchain (which is used to locate clang-format).",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, null),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;

                DryRun = new Option<bool>(
                    "--dry-run",
                    description: "Show a list of files that would be processed, but don't actually make formatting changes.");
            }
        }

        public static Command CreateFormatCommand()
        {
            var options = new Options();
            var command = new Command("format", "Format C++ source code in an Unreal Engine plugin or project.")
            {
                FullDescription = """
                This command formats source code in an Unreal Engine plugin or project using clang-format. If you have a BuildConfig.json file, it'll format source code for all projects across all distributions.

                If you don't have a .clang-format file in the file hierarchy, it'll add one that matches the Unreal Engine code conventions.
                """
            };
            command.AddAllOptions(options);
            command.AddCommonHandler<FormatCommandInstance>(options);
            return command;
        }

        private sealed class FormatCommandInstance : ICommandInstance
        {
            private readonly ILogger<FormatCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly ILocalSdkManager _localSdkManager;
            private readonly IServiceProvider _serviceProvider;
            private readonly Options _options;

            public FormatCommandInstance(
                ILogger<FormatCommandInstance> logger,
                IProcessExecutor processExecutor,
                ILocalSdkManager localSdkManager,
                IServiceProvider serviceProvider,
                Options options)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _localSdkManager = localSdkManager;
                _serviceProvider = serviceProvider;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogWarning("'uet format' is not currently supported on this platform as it relies on using clang-format from the installed Windows SDK.");
                    return 0;
                }

                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var dryRun = context.ParseResult.GetValueForOption(_options.DryRun)!;

                if (dryRun)
                {
                    _logger.LogInformation("--dry-run specified; no changes to source code files will be made (but the relevant SDKs might be installed to locate clang-format).");
                }

                // Detect which directories we should run code formatting for.
                var directories = new List<string>();
                switch (path.Type)
                {
                    case PathSpecType.UPlugin:
                    case PathSpecType.UProject:
                        directories.Add(Path.Combine(path.DirectoryPath, "Source"));
                        break;
                    case PathSpecType.BuildConfig:
                        var loadResult = BuildConfigLoader.TryLoad(
                            _serviceProvider,
                            Path.Combine(path.DirectoryPath, "BuildConfig.json"));
                        switch (loadResult.BuildConfig)
                        {
                            case BuildConfigProject buildConfigProject:
                                foreach (var distribution in buildConfigProject.Distributions)
                                {
                                    directories.Add(Path.Combine(path.DirectoryPath, distribution.FolderName, "Source"));
                                }
                                break;
                            case BuildConfigPlugin buildConfigPlugin:
                                directories.Add(Path.Combine(path.DirectoryPath, buildConfigPlugin.PluginName, "Source"));
                                break;
                        }
                        break;
                }
                if (directories.Count == 0)
                {
                    _logger.LogInformation("No directories for code formatting were detected from the provided --path (or BuildConfig.json).");
                    return 0;
                }

                // Install the relevant Windows SDK if needed, and locate clang-format.exe.
                var packagePath = UetPaths.UetDefaultWindowsSdkStoragePath;
                Directory.CreateDirectory(packagePath);
                var envVars = await _localSdkManager.SetupEnvironmentForSdkSetups(
                    engine.Path!,
                    packagePath,
                    _serviceProvider.GetServices<ISdkSetup>().ToHashSet(),
                    context.GetCancellationToken()).ConfigureAwait(false);
                var clangFormatPath = Path.Combine(envVars["UE_SDKS_ROOT"], "HostWin64", "Win64", "VS2022", "VC", "Tools", "Llvm", "x64", "bin", "clang-format.exe");
                if (!File.Exists(clangFormatPath))
                {
                    _logger.LogError($"Expected clang-format to exist at the following path, but it was not found: {clangFormatPath}");
                    return 1;
                }

                // Now run clang-format on all of the paths, creating .clang-format if it doesn't exist.
                if (directories.Count == 1)
                {
                    _logger.LogInformation("There is 1 directory to run code formatting on:");
                }
                else
                {
                    _logger.LogInformation($"There are {directories.Count} directories to run code formatting on:");
                }
                foreach (var directory in directories)
                {
                    _logger.LogInformation($"- {directory}");
                }
                foreach (var directory in directories)
                {
                    // Ensure .clang-format exists.
                    var clangFormatFilePath = directory;
                    while (clangFormatFilePath != null && !File.Exists(Path.Combine(clangFormatFilePath, ".clang-format")))
                    {
                        clangFormatFilePath = Path.GetDirectoryName(clangFormatFilePath);
                    }
                    if (clangFormatFilePath == null)
                    {
                        if (!dryRun)
                        {
                            _logger.LogInformation($"Creating required .clang-format file: {Path.Combine(directory, ".clang-format")}");
                            File.WriteAllText(Path.Combine(directory, ".clang-format"), @"
---
BasedOnStyle: Microsoft
---
Language: Cpp
AllowShortLambdasOnASingleLine: None
AccessModifierOffset: -4
BinPackArguments: false
BinPackParameters: false
AllowAllArgumentsOnNextLine: false
AllowAllConstructorInitializersOnNextLine: false
AllowAllParametersOfDeclarationOnNextLine: false
AlignAfterOpenBracket: AlwaysBreak
BreakConstructorInitializers: BeforeComma
FixNamespaceComments: false
---
");
                        }
                        else
                        {
                            _logger.LogInformation($"Would create required .clang-format file: {Path.Combine(directory, ".clang-format")}");
                        }
                    }

                    // Find all .cpp and .h files recursively in the target directory, and generate a file list to
                    // execute clang-format.exe with.
                    var fileList = new HashSet<string>();
                    foreach (var file in Directory.EnumerateFiles(directory, "*.cpp", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        fileList.Add(file);
                    }
                    foreach (var file in Directory.EnumerateFiles(directory, "*.h", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true }))
                    {
                        fileList.Add(file);
                    }
                    var tempFileList = Path.GetTempFileName();
                    await File.WriteAllLinesAsync(tempFileList, fileList, context.GetCancellationToken()).ConfigureAwait(false);

                    // Run clang-format.exe.
                    _logger.LogInformation($"Executing clang-format on '{fileList.Count}' files in '{directory}'...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = clangFormatPath,
                            Arguments = dryRun ? ["-i", $"--files={tempFileList}", "--verbose", "--dry-run"] : ["-i", $"--files={tempFileList}", "--verbose"],
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken()).ConfigureAwait(false);
                }

                return 0;
            }
        }
    }
}
