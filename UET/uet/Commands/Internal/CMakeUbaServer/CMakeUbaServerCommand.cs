namespace UET.Commands.Internal.CMakeUbaServer
{
    using CMakeUba;
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.ProcessExecution;
    using Redpoint.ProcessExecution.Enumerable;
    using Redpoint.Uba;
    using Redpoint.Uba.Native;
    using Redpoint.Uet.BuildPipeline.Executors.Engine;
    using Redpoint.Uet.BuildPipeline.Executors;
    using Redpoint.Uet.Workspace;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using UET.Commands.Config;
    using UET.Commands.EngineSpec;
    using static CMakeUba.CMakeUbaService;

    internal class CMakeUbaServerCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;

            public Options()
            {
                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to use for the build.",
                    parseArgument: EngineSpec.ParseEngineSpecContextless(),
                    isDefault: true);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;
            }
        }

        public static Command CreateCMakeUbaServerCommand()
        {
            var options = new Options();
            var command = new Command("cmake-uba-server");
            command.AddAllOptions(options);
            command.AddCommonHandler<CMakeUbaServerCommandInstance>(options, services =>
            {
                services.AddSingleton<IXmlConfigHelper, DefaultXmlConfigHelper>();
            });
            return command;
        }

        private sealed class CMakeUbaServerCommandInstance : CMakeUbaServiceBase, ICommandInstance
        {
            private readonly IUbaServerFactory _ubaServerFactory;
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly IXmlConfigHelper _xmlConfigHelper;
            private readonly ILogger<CMakeUbaServerCommandInstance> _logger;
            private readonly IEngineWorkspaceProvider _engineWorkspaceProvider;
            private readonly Options _options;
            private CancellationToken? _commandCancellationToken;
            private IUbaServer? _ubaServer;

            public CMakeUbaServerCommandInstance(
                IUbaServerFactory ubaServerFactory,
                IGrpcPipeFactory grpcPipeFactory,
                IXmlConfigHelper xmlConfigHelper,
                ILogger<CMakeUbaServerCommandInstance> logger,
                IEngineWorkspaceProvider engineWorkspaceProvider,
                Options options)
            {
                _ubaServerFactory = ubaServerFactory;
                _grpcPipeFactory = grpcPipeFactory;
                _xmlConfigHelper = xmlConfigHelper;
                _logger = logger;
                _engineWorkspaceProvider = engineWorkspaceProvider;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;

                BuildEngineSpecification engineSpec;
                switch (engine.Type)
                {
                    case EngineSpecType.UEFSPackageTag:
                        engineSpec = BuildEngineSpecification.ForUEFSPackageTag(engine.UEFSPackageTag!);
                        break;
                    case EngineSpecType.SESNetworkShare:
                        engineSpec = BuildEngineSpecification.ForSESNetworkShare(engine.SESNetworkShare!);
                        break;
                    case EngineSpecType.RemoteZfs:
                        engineSpec = BuildEngineSpecification.ForRemoteZfs(engine.RemoteZfs!);
                        break;
                    case EngineSpecType.Version:
                        engineSpec = BuildEngineSpecification.ForVersionWithPath(engine.Version!, engine.Path!);
                        break;
                    case EngineSpecType.Path:
                        engineSpec = BuildEngineSpecification.ForAbsolutePath(engine.Path!);
                        break;
                    case EngineSpecType.GitCommit:
                        engineSpec = BuildEngineSpecification.ForGitCommitWithZips(
                            engine.GitUrl!,
                            engine.GitCommit!,
                            engine.ZipLayers,
                            isEngineBuild: false);
                        break;
                    default:
                        throw new NotSupportedException($"The EngineSpecType {engine.Type} is not supported by the 'cmake-uba-server' command.");
                }

                await using ((await _engineWorkspaceProvider.GetEngineWorkspace(
                    engineSpec,
                    string.Empty,
                    context.GetCancellationToken()).ConfigureAwait(false))
                        .AsAsyncDisposable(out var engineWorkspace)
                        .ConfigureAwait(false))
                {
                    // Get the session ID / pipe name.
                    var sessionId = Environment.GetEnvironmentVariable("CMAKE_UBA_SESSION_ID");
                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        _logger.LogError($"Expected CMAKE_UBA_SESSION_ID environment variable to be set.");
                        return 1;
                    }
                    var pipeName = $"cmake-uba-{sessionId}";

                    var ubaDirectory = Path.Combine(engineWorkspace.Path, @"Engine\Binaries\Win64\UnrealBuildAccelerator");
                    UbaNative.Init(ubaDirectory);

                    // Track the timestamp that the server should automatically shut down. This gets moved
                    // forward into the future when we have work in the queue.
                    var shutdownTime = DateTimeOffset.UtcNow.AddSeconds(60);

                    // Create the UBA server.
                    _logger.LogInformation("CMake UBA server is starting up...");
                    var ubaRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)!, "Redpoint", "CMakeUBA");
                    await using (_ubaServerFactory
                        .CreateServer(
                            ubaRoot,
                            Path.Combine(ubaRoot, "UbaTrace.log"))
                        .AsAsyncDisposable(out var ubaServer)
                        .ConfigureAwait(false))
                    {
                        _ubaServer = ubaServer;
                        _commandCancellationToken = context.GetCancellationToken();

                        // Create the gRPC server.
                        await using (_grpcPipeFactory
                            .CreateServer(
                                pipeName,
                                GrpcPipeNamespace.User,
                                this)
                            .AsAsyncDisposable(out var grpcServer)
                            .ConfigureAwait(false))
                        {
                            // Start the gRPC server.
                            await grpcServer.StartAsync().ConfigureAwait(false);
                            _logger.LogInformation("Waiting for incoming requests over gRPC...");

                            // Start the Kubernetes coordinator.
                            using var coordinator = new UbaCoordinatorKubernetes(
                                Path.Combine(ubaDirectory, RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()),
                                _logger,
                                UbaCoordinatorKubernetesConfig.ReadFromBuildConfigurationXml(_xmlConfigHelper));
                            await coordinator.InitAsync(ubaServer).ConfigureAwait(false);
                            try
                            {
                                coordinator.Start();

                                // Every second, evaluate how many processes are in queue / executing locally / executing remotely, 
                                // and use this information to provision agents on Kubernetes as needed.
                                try
                                {
                                    while (true)
                                    {
                                        _logger.LogDebug($"Pending: {ubaServer.ProcessesPendingInQueue} Executing Locally: {ubaServer.ProcessesExecutingLocally} Executing Remotely: {ubaServer.ProcessesExecutingRemotely}");

                                        if (ubaServer.ProcessesPendingInQueue > 0 || ubaServer.ProcessesExecutingLocally > 0 || ubaServer.ProcessesExecutingRemotely > 0)
                                        {
                                            shutdownTime = DateTimeOffset.UtcNow.AddSeconds(60);
                                        }
                                        if (shutdownTime < DateTimeOffset.UtcNow)
                                        {
                                            _logger.LogInformation("CMake UBA server is shutting down because there hasn't been any requests recently...");
                                            return 0;
                                        }

                                        await Task.Delay(1000, context.GetCancellationToken()).ConfigureAwait(false);
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                }

                                // Stop the gRPC server.
                                _logger.LogInformation("CMake UBA server is shutting down...");
                                await grpcServer.StopAsync().ConfigureAwait(false);
                            }
                            finally
                            {
                                await coordinator.CloseAsync().ConfigureAwait(false);
                            }
                        }
                    }
                }

                return 0;
            }

            public override async Task ExecuteProcess(ProcessRequest request, IServerStreamWriter<CMakeUba.ProcessResponse> responseStream, ServerCallContext context)
            {
                if (!_commandCancellationToken.HasValue ||
                    _ubaServer == null)
                {
                    await responseStream.WriteAsync(new CMakeUba.ProcessResponse
                    {
                        StandardOutputLine = "(The CMake UBA server is not in a valid state!)",
                    }).ConfigureAwait(false);
                    await responseStream.WriteAsync(new CMakeUba.ProcessResponse
                    {
                        ExitCode = 99999,
                    }).ConfigureAwait(false);
                    return;
                }

                var specification = new UbaProcessSpecification
                {
                    FilePath = request.Path,
                    Arguments = request.Arguments.Select(x => new LogicalProcessArgument(x.LogicalValue)).ToArray(),
                    WorkingDirectory = request.WorkingDirectory,
                    PreferRemote = request.PreferRemote,
                    AllowRemote = string.Equals(Path.GetFileName(request.Path), "clang-cl.exe", StringComparison.OrdinalIgnoreCase),
                };

                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(_commandCancellationToken.Value, context.CancellationToken);

                await foreach (var response in _ubaServer.ExecuteAsync(specification, context.CancellationToken))
                {
                    switch (response)
                    {
                        case StandardOutputResponse stdout:
                            await responseStream.WriteAsync(new CMakeUba.ProcessResponse
                            {
                                StandardOutputLine = stdout.Data,
                            }).ConfigureAwait(false);
                            break;
                        case StandardErrorResponse stderr:
                            await responseStream.WriteAsync(new CMakeUba.ProcessResponse
                            {
                                StandardOutputLine = stderr.Data,
                            }).ConfigureAwait(false);
                            break;
                        case ExitCodeResponse exitCode:
                            await responseStream.WriteAsync(new CMakeUba.ProcessResponse
                            {
                                ExitCode = exitCode.ExitCode,
                            }).ConfigureAwait(false);
                            // @note: This is the last message; we're done.
                            return;
                    }
                }
            }

            public override Task<EmptyMessage> PingServer(EmptyMessage request, ServerCallContext context)
            {
                return Task.FromResult(new EmptyMessage());
            }
        }
    }
}
