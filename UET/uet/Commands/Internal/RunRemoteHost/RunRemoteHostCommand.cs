namespace UET.Commands.Internal.RunRemoteHost
{
    using Grpc.Core;
    using Redpoint.AutoDiscovery;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using Redpoint.ProcessExecution.Enumerable;
    using RemoteHostApi;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;
    using static RemoteHostApi.RemoteHostService;
    using Microsoft.Extensions.Logging;
    using System.CommandLine.IO;

    internal class RunRemoteHostCommand
    {
        public sealed class Options
        {
        }

        public static Command CreateRunRemoteHostCommand()
        {
            var options = new Options();
            var command = new Command("run-remote-host");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunRemoteHostCommandInstance>(options);
            return command;
        }

        private sealed class RunRemoteHostCommandInstance : RemoteHostServiceBase, ICommandInstance, IDisposable
        {
            private readonly ILogger<RunRemoteHostCommandInstance> _logger;
            private readonly INetworkAutoDiscovery _networkAutoDiscovery;
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly IProcessExecutor _processExecutor;
            private readonly SemaphoreSlim _semaphore;

            public RunRemoteHostCommandInstance(
                ILogger<RunRemoteHostCommandInstance> logger,
                INetworkAutoDiscovery networkAutoDiscovery,
                IGrpcPipeFactory grpcPipeFactory,
                IProcessExecutor processExecutor)
            {
                _logger = logger;
                _networkAutoDiscovery = networkAutoDiscovery;
                _grpcPipeFactory = grpcPipeFactory;
                _processExecutor = processExecutor;
                _semaphore = new SemaphoreSlim(1);
            }

            public void Dispose()
            {
                _semaphore.Dispose();
            }

            public override async Task RunProcess(
                RunProcessRequest request,
                IServerStreamWriter<RunProcessResponse> responseStream,
                ServerCallContext context)
            {
                if (!context.Peer.StartsWith("ipv4:", StringComparison.Ordinal))
                {
                    throw new RpcException(new Status(StatusCode.InvalidArgument, "Expected caller to be an IPv4 address."));
                }

                if (!_semaphore.Wait(0))
                {
                    throw new RpcException(new Status(StatusCode.Unavailable, "This host is currently executing another command."));
                }
                try
                {
                    var ip = IPEndPoint.Parse(context.Peer.AsSpan(5));

                    string ConvertPath(string path)
                    {
                        var drive = Path.GetPathRoot(path)!;
                        return $"\\\\{ip.Address}\\{drive[0]}$\\{path[drive.Length..]}";
                    }

                    _logger.LogInformation($"Attempting to run '{ConvertPath(request.Path)}'...");

                    await foreach (var ev in
                        _processExecutor.ExecuteAsync(new ProcessSpecification
                        {
                            FilePath = ConvertPath(request.Path),
                            Arguments = request.Arguments.ToArray(),
                            WorkingDirectory = ConvertPath(request.WorkingDirectory),
                        },
                        context.CancellationToken))
                    {
                        switch (ev)
                        {
                            case StandardOutputResponse standardOutput:
                                _logger.LogInformation(standardOutput.Data);
                                await responseStream.WriteAsync(new RunProcessResponse
                                {
                                    StandardOutputLine = standardOutput.Data,
                                }, context.CancellationToken).ConfigureAwait(false);
                                break;
                            case StandardErrorResponse standardError:
                                _logger.LogInformation(standardError.Data);
                                await responseStream.WriteAsync(new RunProcessResponse
                                {
                                    StandardErrorLine = standardError.Data,
                                }, context.CancellationToken).ConfigureAwait(false);
                                break;
                            case ExitCodeResponse exitCode:
                                _logger.LogInformation($"Process exited with exit code {exitCode.ExitCode}.");
                                await responseStream.WriteAsync(new RunProcessResponse
                                {
                                    ExitCode = exitCode.ExitCode,
                                }, context.CancellationToken).ConfigureAwait(false);
                                break;
                        }
                    }
                }
                finally
                {
                    _semaphore.Release();
                }
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                try
                {
                    await using (_grpcPipeFactory.CreateNetworkServer(this).AsAsyncDisposable(out var server).ConfigureAwait(false))
                    {
                        await server.StartAsync().ConfigureAwait(false);

                        _logger.LogInformation($"'run remote' service has started on port {server.NetworkPort}.");

                        await _networkAutoDiscovery.RegisterServiceAsync($"{Guid.NewGuid()}._uet-run-remote._tcp.local", server.NetworkPort, context.GetCancellationToken()).ConfigureAwait(false);

                        _logger.LogInformation($"Registered with network discovery.");

                        while (!context.GetCancellationToken().IsCancellationRequested)
                        {
                            await Task.Delay(1000, context.GetCancellationToken()).ConfigureAwait(false);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("'run remote' service has stopped.");
                }

                return 0;
            }
        }
    }
}
