namespace UET.Commands.Generate
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using UET.Commands.Build;
    using UET.Commands.EngineSpec;

    internal class GenerateCommand
    {
        internal class Options
        {
            public Option<EngineSpec> Engine;
            public Option<PathSpec> Path;

            public Options()
            {
                Path = new Option<PathSpec>(
                    "--path",
                    description: "The directory path that contains a .uproject file. If this parameter isn't provided, defaults to the current working directory.",
                    parseArgument: PathSpec.ParsePathSpec,
                    isDefault: true);
                Path.AddAlias("-p");
                Path.Arity = ArgumentArity.ExactlyOne;

                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to target the generated project files at.",
                    parseArgument: EngineSpec.ParseEngineSpec(Path, null),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateGenerateCommand()
        {
            var options = new Options();
            var command = new Command("generate", "Generate Visual Studio or Xcode project files for an Unreal Engine project.");
            command.AddAllOptions(options);
            command.AddCommonHandler<GenerateCommandInstance>(options);
            return command;
        }

        private class GenerateCommandInstance : ICommandInstance
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

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;
                var path = context.ParseResult.GetValueForOption(_options.Path)!;

                if (engine.Path == null)
                {
                    _logger.LogError("Project files can't be generated for a UEFS or Git mounted engine. If you want to use this type of engine, use 'uet uefs' to mount it first, and then pass the path to --engine instead.");
                    return 1;
                }

                if (path.Type != PathSpecType.UProject)
                {
                    _logger.LogError("Generating project files only works for .uproject files. Run this command in a directory that contains a .uproject file, or pass the path to a .uproject file with --path.");
                    return 1;
                }

                ProcessSpecification processSpecification;
                if (OperatingSystem.IsWindows())
                {
                    processSpecification = new ProcessSpecification
                    {
                        FilePath = Path.Combine(engine.Path, "Engine", "Build", "BatchFiles", "Build.bat"),
                        Arguments = new[]
                        {
                            "-projectfiles",
                            $"-project={path.UProjectPath}",
                            "-game",
                            "-engine",
                            "-rocket",
                        },
                        WorkingDirectory = Path.GetDirectoryName(path.UProjectPath),
                    };
                }
                else if (OperatingSystem.IsMacOS())
                {
                    processSpecification = new ProcessSpecification
                    {
                        FilePath = Path.Combine(engine.Path, "Engine", "Build", "BatchFiles", "Mac", "Build.sh"),
                        Arguments = new[]
                        {
                            "-projectfiles",
                            $"-project={path.UProjectPath}",
                            "-game",
                            "-engine",
                            "-rocket",
                        },
                        WorkingDirectory = Path.GetDirectoryName(path.UProjectPath),
                    };
                }
                else if (OperatingSystem.IsLinux())
                {
                    processSpecification = new ProcessSpecification
                    {
                        FilePath = Path.Combine(engine.Path, "Engine", "Build", "BatchFiles", "Linux", "Build.sh"),
                        Arguments = new[]
                        {
                            "-projectfiles",
                            $"-project={path.UProjectPath}",
                            "-game",
                            "-engine",
                            "-rocket",
                        },
                        WorkingDirectory = Path.GetDirectoryName(path.UProjectPath),
                    };
                }
                else
                {
                    _logger.LogError("Generating project files is not supported on this platform.");
                    return 1;
                }

                var exitCode = await _processExecutor.ExecuteAsync(
                    processSpecification,
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken()).ConfigureAwait(false);
                return exitCode;
            }
        }
    }
}
