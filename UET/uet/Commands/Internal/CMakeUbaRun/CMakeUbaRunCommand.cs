namespace UET.Commands.Internal.CMakeUbaRun
{
    using CMakeUba;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;
    using UET.Services;
    using static CMakeUba.CMakeUbaService;
    using Semaphore = System.Threading.Semaphore;

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

            private async Task<bool> IsServerRunningAsync()
            {
                var client = _grpcPipeFactory.CreateClient(
                    "cmake-uba-server",
                    GrpcPipeNamespace.User,
                    channel => new CMakeUbaServiceClient(channel));
                try
                {
                    await client.PingServerAsync(new CMakeUba.EmptyMessage()).ConfigureAwait(false);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            private async Task WaitForServerToStartAsync(CancellationToken serverUnexpectedExit)
            {
                while (true)
                {
                    serverUnexpectedExit.ThrowIfCancellationRequested();

                    var client = _grpcPipeFactory.CreateClient(
                        "cmake-uba-server",
                        GrpcPipeNamespace.User,
                        channel => new CMakeUbaServiceClient(channel));
                    try
                    {
                        await client.PingServerAsync(new CMakeUba.EmptyMessage(), cancellationToken: serverUnexpectedExit).ConfigureAwait(false);
                        return;
                    }
                    catch
                    {
                        await Task.Delay(1000, serverUnexpectedExit).ConfigureAwait(false);
                    }
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                // Start the CMake UBA server on-demand if it's not already running.
                if (!await IsServerRunningAsync().ConfigureAwait(false))
                {
                    using var globalServerSemaphore = new Semaphore(1, 1, "cmake-uba-grpc-server");
                    globalServerSemaphore.WaitOne();
                    try
                    {
                        if (!await IsServerRunningAsync().ConfigureAwait(false))
                        {
                            var cts = new CancellationTokenSource();
                            var ctsDisposed = new Gate();
                            try
                            {
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        _logger.LogInformation("Starting CMake UBA server. You will not see output or errors from it. To see diagnostic information, run 'uet internal cmake-uba-server' in a separate terminal.");
                                        await _processExecutor.ExecuteAsync(
                                            new ProcessSpecification
                                            {
                                                FilePath = _selfLocation.GetUetLocalLocation(),
                                                Arguments = ["internal", "cmake-uba-server", "--auto-close"]
                                            },
                                            CaptureSpecification.Silence,
                                            CancellationToken.None).ConfigureAwait(false);
                                    }
                                    finally
                                    {
                                        if (!ctsDisposed.Opened)
                                        {
                                            cts.Cancel();
                                        }
                                    }
                                });

                                await WaitForServerToStartAsync(cts.Token).ConfigureAwait(false);
                            }
                            finally
                            {
                                ctsDisposed.Open();
                                cts.Dispose();
                            }
                        }
                    }
                    finally
                    {
                        globalServerSemaphore.Release();
                    }
                }

                // Create our gRPC client.
                var client = _grpcPipeFactory.CreateClient(
                    "cmake-uba-server",
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
