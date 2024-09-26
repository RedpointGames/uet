namespace UET.Commands.Internal.CMakeUbaRun
{
    using CMakeUba;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Services;
    using static CMakeUba.CMakeUbaService;

    internal class CMakeUbaRunCommand
    {
        internal sealed class Options
        {
            public Option<bool> PreferRemote = new Option<bool>(
                "--prefer-remote",
                "If true, the CMake UBA server will prefer to run this command remotely.");
        }

        public static Command CreateCMakeUbaRunCommand()
        {
            var options = new Options();
            var commandArguments = new Argument<string[]>("command-and-arguments", "The command to run, followed by any arguments to pass to it.");
            commandArguments.Arity = ArgumentArity.OneOrMore;
            var command = new Command("cmake-uba-run");
            command.AddAllOptions(options);
            command.AddArgument(commandArguments);
            command.AddCommonHandler<CMakeUbaRunCommandInstance>(options, services =>
            {
                services.AddSingleton(commandArguments);
            });
            return command;
        }

        private sealed class CMakeUbaRunCommandInstance : ICommandInstance
        {
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly ILogger<CMakeUbaRunCommandInstance> _logger;
            private readonly ISelfLocation _selfLocation;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;
            private readonly Argument<string[]> _commandArguments;

            public CMakeUbaRunCommandInstance(
                IGrpcPipeFactory grpcPipeFactory,
                ILogger<CMakeUbaRunCommandInstance> logger,
                ISelfLocation selfLocation,
                IProcessExecutor processExecutor,
                Options options,
                Argument<string[]> commandArguments)
            {
                _grpcPipeFactory = grpcPipeFactory;
                _logger = logger;
                _selfLocation = selfLocation;
                _processExecutor = processExecutor;
                _options = options;
                _commandArguments = commandArguments;
            }

            private async Task<bool> IsServerRunningAsync(string pipeName)
            {
                for (int i = 0; i < 10; i++)
                {
                    var client = _grpcPipeFactory.CreateClient(
                        pipeName,
                        GrpcPipeNamespace.User,
                        channel => new CMakeUbaServiceClient(channel));
                    try
                    {
                        await client.PingServerAsync(new CMakeUba.EmptyMessage()).ConfigureAwait(false);
                        return true;
                    }
                    catch
                    {
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }
                return false;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var sessionId = Environment.GetEnvironmentVariable("CMAKE_UBA_SESSION_ID");
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    _logger.LogError($"Expected CMAKE_UBA_SESSION_ID environment variable to be set.");
                    return 1;
                }
                var pipeName = $"cmake-uba-{sessionId}";

                // Start the CMake UBA server on-demand if it's not already running.
                if (!await IsServerRunningAsync(pipeName).ConfigureAwait(false))
                {
                    _logger.LogError($"The CMake UBA server isn't running. Start it with '{_selfLocation.GetUetLocalLocation()} internal cmake-uba-server &'");
                    return 1;
                }

                // Create our gRPC client.
                var client = _grpcPipeFactory.CreateClient(
                    pipeName,
                    GrpcPipeNamespace.User,
                    channel => new CMakeUbaServiceClient(channel));

                // Run the process.
                var arguments = context.ParseResult.GetValueForArgument(_commandArguments)!;
                var request = new CMakeUba.ProcessRequest
                {
                    Path = arguments[0],
                    WorkingDirectory = Environment.CurrentDirectory,
                    PreferRemote = context.ParseResult.GetValueForOption(_options.PreferRemote),
                };
                for (int i = 1; i < arguments.Length; i++)
                {
                    request.Arguments.Add(new ProcessArgument { LogicalValue = arguments[i] });
                }

                var response = client.ExecuteProcess(request, cancellationToken: context.GetCancellationToken());

                // Stream the response values.
                while (await response.ResponseStream.MoveNext(cancellationToken: context.GetCancellationToken()).ConfigureAwait(false))
                {
                    switch (response.ResponseStream.Current.DataCase)
                    {
                        case ProcessResponse.DataOneofCase.StandardOutputLine:
                            Console.WriteLine(response.ResponseStream.Current.StandardOutputLine);
                            break;
                        case ProcessResponse.DataOneofCase.ExitCode:
                            // @note: This is the last response; return the exit code.
                            return response.ResponseStream.Current.ExitCode;
                    }
                }

                _logger.LogError("Did not receive exit code for process from CMake UBA server before the response stream ended.");
                return 1;
            }
        }
    }
}
