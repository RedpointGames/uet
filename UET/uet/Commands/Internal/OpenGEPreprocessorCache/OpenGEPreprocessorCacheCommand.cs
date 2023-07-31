namespace UET.Commands.Internal.OpenGEPreprocessorCache
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using PreprocessorCacheApi;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.PreprocessorCache;
    using Redpoint.Reservation;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading.Tasks;

    internal class OpenGEPreprocessorCacheCommand
    {
        internal class Options
        {
        }

        public static Command CreateOpenGEPreprocessorCacheCommand()
        {
            var options = new Options();
            var command = new Command("openge-preprocessor-cache");
            command.AddAllOptions(options);
            command.AddCommonHandler<OpenGEPreprocessorCacheCommandInstance>(options);
            return command;
        }

        private class OpenGEPreprocessorCacheCommandInstance : PreprocessorCache.PreprocessorCacheBase, ICommandInstance
        {
            private readonly IGrpcPipeFactory _grpcPipeFactory;
            private readonly ICachingPreprocessorScannerFactory _cachingPreprocessorScannerFactory;
            private readonly IReservationManagerFactory _reservationManagerFactory;
            private readonly ILogger<OpenGEPreprocessorCacheCommandInstance> _logger;
            private ICachingPreprocessorScanner? _cachingScanner;
            private DateTimeOffset _lastUsedUtc;

            public OpenGEPreprocessorCacheCommandInstance(
                IGrpcPipeFactory grpcPipeFactory,
                ICachingPreprocessorScannerFactory cachingPreprocessorScannerFactory,
                IReservationManagerFactory reservationManagerFactory,
                ILogger<OpenGEPreprocessorCacheCommandInstance> logger)
            {
                _grpcPipeFactory = grpcPipeFactory;
                _cachingPreprocessorScannerFactory = cachingPreprocessorScannerFactory;
                _reservationManagerFactory = reservationManagerFactory;
                _logger = logger;
                _lastUsedUtc = DateTimeOffset.UtcNow;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var dataDirectory = true switch
                {
                    var v when v == OperatingSystem.IsWindows() => Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "OpenGE",
                        "Cache"),
                    var v when v == OperatingSystem.IsMacOS() => Path.Combine("/Users", "Shared", "OpenGE", "Cache"),
                    var v when v == OperatingSystem.IsLinux() => Path.Combine("/tmp", "OpenGE", "Cache"),
                    _ => throw new PlatformNotSupportedException(),
                };
                var reservationManager = _reservationManagerFactory.CreateReservationManager(dataDirectory);

                var reservation = await reservationManager.TryReserveExactAsync("Preprocessor");
                if (reservation == null)
                {
                    _logger.LogInformation("Another instance of the OpenGE preprocessor cache is running.");
                    return 0;
                }
                IGrpcPipeServer<OpenGEPreprocessorCacheCommandInstance>? server = null;
                try
                {
                    await using (reservation)
                    {
                        _logger.LogInformation("Starting OpenGE preprocessor cache...");

                        using (_cachingScanner = _cachingPreprocessorScannerFactory.CreateCachingPreprocessorScanner(reservation.ReservedPath))
                        {
                            server = _grpcPipeFactory.CreateServer(
                                "OpenGEPreprocessorCache",
                                GrpcPipeNamespace.Computer,
                                this);
                            await server.StartAsync();

                            _lastUsedUtc = DateTimeOffset.UtcNow;

                            // Run until terminated, or until we've been idle for 5 minutes.
                            while (!context.GetCancellationToken().IsCancellationRequested)
                            {
                                await Task.Delay(10000, context.GetCancellationToken());
                                if ((DateTimeOffset.UtcNow - _lastUsedUtc).TotalMinutes > 5)
                                {
                                    _logger.LogInformation("OpenGE preprocessor cache is automatically exiting because it has been idle for more than 5 minutes.");
                                    return 0;
                                }
                            }
                        }

                        return 0;
                    }
                }
                finally
                {
                    // We shutdown the server outside the reservation so that we can't run into
                    // a race condition where a new instance of the preprocessor cache exits
                    // due the reservation being held while the current one is shutting down.
                    if (server != null)
                    {
                        await server.StopAsync();
                    }
                }
            }

            public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
            {
                return Task.FromResult(new PingResponse());
            }

            public override async Task<GetUnresolvedDependenciesResponse> GetUnresolvedDependencies(
                GetUnresolvedDependenciesRequest request,
                ServerCallContext context)
            {
                _lastUsedUtc = DateTimeOffset.UtcNow;
                var result = await _cachingScanner!.ParseIncludes(
                    request.Path,
                    context.CancellationToken);
                var response = new GetUnresolvedDependenciesResponse
                {
                    Result = result,
                };
                _lastUsedUtc = DateTimeOffset.UtcNow;
                return response;
            }

            public override Task<GetResolvedDependenciesResponse> GetResolvedDependencies(
                GetResolvedDependenciesRequest request,
                ServerCallContext context)
            {
                throw new RpcException(new Status(StatusCode.Unimplemented, "GetResolvedDependencies is not implemented yet."));
            }
        }
    }
}
