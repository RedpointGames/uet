namespace UET.Commands.Internal.CMakeUbaRun
{
    using CMakeUba;
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Services;
    using static CMakeUba.CMakeUbaService;

    internal class CMakeUbaRunCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<CMakeUbaRunCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("cmake-uba-run");
                })
            .Build();

        internal sealed class Options
        {
            public Option<bool> PreferRemote = new Option<bool>(
                "--prefer-remote",
                "If true, the CMake UBA server will prefer to run this command remotely.");

            public Argument<string[]> CommandArguments = new Argument<string[]>("command-and-arguments", "The command to run, followed by any arguments to pass to it.")
            {
                Arity = ArgumentArity.OneOrMore,
            };
        }

        private sealed class CMakeUbaRunCommandInstance : ICommandInstance
        {
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly ILogger<CMakeUbaRunCommandInstance> _logger;
            private readonly ISelfLocation _selfLocation;
            private readonly IProcessExecutor _processExecutor;
            private readonly Options _options;

            public CMakeUbaRunCommandInstance(
                IGrpcPipeFactory grpcPipeFactory,
                ILogger<CMakeUbaRunCommandInstance> logger,
                ISelfLocation selfLocation,
                IProcessExecutor processExecutor,
                Options options)
            {
                _grpcPipeFactory = grpcPipeFactory;
                _logger = logger;
                _selfLocation = selfLocation;
                _processExecutor = processExecutor;
                _options = options;
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

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var sessionId = Environment.GetEnvironmentVariable("CMAKE_UBA_SESSION_ID");
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    _logger.LogError($"Expected CMAKE_UBA_SESSION_ID environment variable to be set.");
                    return 1;
                }
                var pipeName = $"cmake-uba-{sessionId}";

            retryConnection:
                try
                {
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
                    var arguments = context.ParseResult.GetValueForArgument(_options.CommandArguments)!;
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
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
                {
                    goto retryConnection;
                }
            }
        }
    }
}
