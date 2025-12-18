namespace UET.Commands.CMake
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Commands.ParameterSpec;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using UET.Services;

    internal sealed class CMakeCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;

            public Options()
            {
                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use the Unreal Build Accelerator from.",
                    parseArgument: EngineSpec.ParseEngineSpecContextless(),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ZeroOrOne;
            }
        }

        public static Command CreateCMakeCommand()
        {
            var options = new Options();
            var commandArguments = new Argument<string[]>("command-and-arguments", "The command to run, followed by any arguments to pass to it.");
            commandArguments.Arity = ArgumentArity.ZeroOrMore;
            var command = new Command("cmake", "Run a CMake-based build leveraging the Unreal Build Accelerator (UBA) to distribute compilation.")
            {
                FullDescription = """
                This command runs a CMake-based build, leveraging the Unreal Build Accelerator (UBA) to distribute the build over Kubernetes.
                
                -------------

                Before you can distribute builds, you must configure your BuildConfiguration.xml file located at "%appdata%\Unreal Engine\UnrealBuildTool\BuildConfiguration.xml" with the settings to connect to the Kubernetes cluster. The Kubernetes cluster must have Windows nodes in it. You can use RKM (https://src.redpoint.games/redpointgames/rkm) to spin up a Kubernetes cluster with Windows nodes with a single command.

                <?xml version="1.0" encoding="utf-8"?>
                <Configuration xmlns="https://www.unrealengine.com/BuildConfiguration">
                  <Kubernetes>
                    <Namespace>default</Namespace>
                    <Context>defafult</Context>
                    <SmbServer>10.0.0.100</SmbServer>
                    <SmbShare>ShareName</SmbShare>
                    <SmbUsername>Domain\Username</SmbUsername>
                    <SmbPassword>Password</SmbPassword>
                  </Kubernetes>
                </Configuration>

                The 'Smb' settings specify a network share that all Windows nodes can access as the specified user. The Unreal Build Accelerator will be copied to this share and the containers will copy from this network share.

                Your `kubectl` configuration must be connected to the cluster already, as per the 'Context' setting. You can get the context name by running `kubectl config get-contexts`. The 'Namespace' setting specifies what Kubernetes namespace to launch UBA agents into.

                -------------

                To distribute builds, you must first generate your CMake project using:
                
                uet cmake -- ...
                
                You should omit `-G`; this command will automatically select the Ninja project generator which is required to distribute builds.

                Once you've generated your project, you can distribute the build using:

                uet cmake -e 5.5 -- --build ...

                The presence of `--build` in the CMake arguments is what this tool uses to determine whether CMake is generating project files or running the build. You only need to specify `-e` as an argument to this command when running the build; it is not necessary during generation.

                All arguments past the `--` are forwarded to CMake intact.
                """
            };
            command.AddAllOptions(options);
            command.AddArgument(commandArguments);
            command.AddCommonHandler<CMakeCommandInstance>(options, services =>
            {
                services.AddSingleton(commandArguments);
            });
            return command;
        }

        private sealed class CMakeCommandInstance : ICommandInstance
        {
            private readonly ILogger<CMakeCommandInstance> _logger;
            private readonly IProcessExecutor _processExecutor;
            private readonly ISelfLocation _selfLocation;
            private readonly IPathResolver _pathResolver;
            private readonly Options _options;
            private readonly Argument<string[]> _commandArguments;

            public CMakeCommandInstance(
                ILogger<CMakeCommandInstance> logger,
                IProcessExecutor processExecutor,
                ISelfLocation selfLocation,
                IPathResolver pathResolver,
                Options options,
                Argument<string[]> commandArguments)
            {
                _logger = logger;
                _processExecutor = processExecutor;
                _selfLocation = selfLocation;
                _pathResolver = pathResolver;
                _options = options;
                _commandArguments = commandArguments;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var extraArguments = context.ParseResult.GetValueForArgument(_commandArguments);

                string? cmake = null;
                try
                {
                    cmake = await _pathResolver.ResolveBinaryPath("cmake");
                }
                catch (FileNotFoundException)
                {
                    cmake = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        "Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\CommonExtensions\\Microsoft\\CMake\\CMake\\bin\\cmake.exe");
                }
                if (cmake == null || !File.Exists(cmake))
                {
                    _logger.LogError($"Unable to find CMake on PATH or at '{cmake}'.");
                    return 1;
                }

                var engineResult = context.ParseResult.CommandResult.FindResultFor(_options.Engine);
                string? engineString = null;
                if (engineResult != null)
                {
                    // Run the parse of the engine specification so we can error early if it's invalid.
                    context.ParseResult.GetValueForOption(_options.Engine);

                    engineString = engineResult.Tokens.Count > 0 ? engineResult.Tokens[0].Value : null;
                }

                if (extraArguments.Contains("--build"))
                {
                    _logger.LogInformation("CMake is building the project (--build detected).");

                    if (string.IsNullOrWhiteSpace(engineString))
                    {
                        _logger.LogError("Missing --engine option, which is necessary for --build. Use this command like 'uet cmake -e 5.5 -- ...'.");
                        return 1;
                    }

                    // Generate a session ID.
                    var sessionId = Guid.NewGuid().ToString();

                    // Start the CMake UBA server.
                    _logger.LogInformation("Starting CMake UBA server...");
                    using var backgroundCts = CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken());
                    var backgroundTask = Task.Run(async () => await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = _selfLocation.GetUetLocalLocation(),
                            Arguments = [
                                "internal",
                                "cmake-uba-server",
                                "-e",
                                engineString,
                            ],
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "CMAKE_UBA_SESSION_ID", sessionId }
                            }
                        },
                        CaptureSpecification.Passthrough,
                        backgroundCts.Token).ConfigureAwait(false));

                    var appendArguments = new List<LogicalProcessArgument>();
                    if (!(extraArguments.Contains("-j") || extraArguments.Any(x => x.StartsWith("-j", StringComparison.Ordinal))))
                    {
                        // Override core detection because we're distributing builds.
                        appendArguments.Add("-j256");
                    }

                    // Run CMake.
                    try
                    {
                        _logger.LogInformation("Running CMake...");
                        return await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = cmake,
                                Arguments = extraArguments.Select(x => new LogicalProcessArgument(x)).Concat(appendArguments),
                                EnvironmentVariables = new Dictionary<string, string>
                                {
                                    { "CMAKE_UBA_SESSION_ID", sessionId },
                                    { "UET_IMPLICIT_COMMAND", "cmake-uba-run" },
                                }
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                    }
                    finally
                    {
                        backgroundCts.Cancel();
                        try
                        {
                            _logger.LogInformation("Stopping CMake UBA server...");
                            await backgroundTask.ConfigureAwait(false);
                        }
                        catch
                        {
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("CMake is generating project files (--build not detected).");

                    if (extraArguments.Contains("-G") || extraArguments.Any(x => x.StartsWith("-G", StringComparison.Ordinal)))
                    {
                        _logger.LogError("Detected -G argument passed to CMake during project generation. Omit this setting as UET will force use of the Ninja build system.");
                        return 1;
                    }

                    // Run CMake.
                    _logger.LogInformation("Running CMake...");
                    return await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = cmake,
                            Arguments = new LogicalProcessArgument[]
                            {
                                "-G",
                                "Ninja",
                                $"-DCMAKE_C_COMPILER_LAUNCHER={_selfLocation.GetUetLocalLocation()}",
                                $"-DCMAKE_CXX_COMPILER_LAUNCHER={_selfLocation.GetUetLocalLocation()}",
                            }.Concat(extraArguments.Select(x => new LogicalProcessArgument(x))),
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                }
            }
        }
    }
}
