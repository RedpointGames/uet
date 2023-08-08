namespace Redpoint.OpenGE.Component.PreprocessorCache
{
    using Grpc.Core;
    using Redpoint.OpenGE.Component.PreprocessorCache.DependencyResolution;
    using Redpoint.OpenGE.Component.PreprocessorCache.DirectiveScanner;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class InProcessPreprocessorCache : AbstractInProcessPreprocessorCache
    {
        private readonly ICachingPreprocessorScannerFactory _cachingPreprocessorScannerFactory;
        private readonly IPreprocessorResolver _preprocessorResolver;
        private readonly IReservationManagerFactory _reservationManagerFactory;
        private readonly SemaphoreSlim _initSemaphore = new SemaphoreSlim(1);
        private ICachingPreprocessorScanner? _cachingScanner;
        private bool _inited = false;
        private bool _disposed = false;
        private IReservation? _reservation;

        public InProcessPreprocessorCache(
            ICachingPreprocessorScannerFactory cachingPreprocessorScannerFactory,
            IPreprocessorResolver preprocessorResolver,
            IReservationManagerFactory reservationManagerFactory)
        {
            _cachingPreprocessorScannerFactory = cachingPreprocessorScannerFactory;
            _preprocessorResolver = preprocessorResolver;
            _reservationManagerFactory = reservationManagerFactory;
        }

        public override DateTimeOffset LastGrpcRequestUtc { get; protected set; } = DateTimeOffset.UtcNow;

        public async override ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InProcessPreprocessorCache));
            }
            await _initSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(InProcessPreprocessorCache));
                }
                if (!_inited)
                {
                    _disposed = true;
                    return;
                }
                if (_cachingScanner != null)
                {
                    _cachingScanner.Dispose();
                    _cachingScanner = null;
                }
                if (_reservation != null)
                {
                    await _reservation.DisposeAsync();
                    _reservation = null;
                }
                _disposed = true;
                _inited = false;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        public async override Task EnsureAsync()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InProcessPreprocessorCache));
            }
            if (_inited)
            {
                return;
            }
            await _initSemaphore.WaitAsync();
            try
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(InProcessPreprocessorCache));
                }
                if (_inited)
                {
                    return;
                }

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

                _reservation = await reservationManager.TryReserveExactAsync("Preprocessor");
                if (_reservation == null)
                {
                    throw new PreprocessorCacheAlreadyRunningException();
                }

                _cachingScanner = _cachingPreprocessorScannerFactory.CreateCachingPreprocessorScanner(_reservation.ReservedPath);

                _inited = true;
            }
            finally
            {
                _initSemaphore.Release();
            }
        }

        public async override Task<PreprocessorResolutionResultWithTimingMetadata> GetResolvedDependenciesAsync(
            string filePath, 
            string[] forceIncludesFromPch,
            string[] forceIncludes, 
            string[] includeDirectories,
            Dictionary<string, string> globalDefinitions, 
            CancellationToken cancellationToken)
        {
            await EnsureAsync();
            return await _preprocessorResolver.ResolveAsync(
                _cachingScanner!,
                filePath,
                forceIncludesFromPch,
                forceIncludes,
                includeDirectories,
                globalDefinitions,
                cancellationToken);
        }

        public async override Task<PreprocessorScanResultWithCacheMetadata> GetUnresolvedDependenciesAsync(
            string filePath, 
            CancellationToken cancellationToken)
        {
            await EnsureAsync();
            return await _cachingScanner!.ParseIncludes(
                filePath,
                cancellationToken);
        }

        public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
        {
            return Task.FromResult(new PingResponse());
        }

        public override async Task<GetUnresolvedDependenciesResponse> GetUnresolvedDependencies(
            GetUnresolvedDependenciesRequest request,
            ServerCallContext context)
        {
            LastGrpcRequestUtc = DateTimeOffset.UtcNow;
            var result = await _cachingScanner!.ParseIncludes(
                request.Path,
                context.CancellationToken);
            var response = new GetUnresolvedDependenciesResponse
            {
                Result = result,
            };
            LastGrpcRequestUtc = DateTimeOffset.UtcNow;
            return response;
        }

        public override async Task<GetResolvedDependenciesResponse> GetResolvedDependencies(
            GetResolvedDependenciesRequest request,
            ServerCallContext context)
        {
            try
            {
                LastGrpcRequestUtc = DateTimeOffset.UtcNow;
                var result = await _preprocessorResolver.ResolveAsync(
                    _cachingScanner!,
                    request.Path,
                    request.ForceIncludeFromPchPaths.ToArray(),
                    request.ForceIncludePaths.ToArray(),
                    request.IncludeDirectories.ToArray(),
                    request.GlobalDefinitions.ToDictionary(k => k.Key, v => v.Value),
                    context.CancellationToken);
                LastGrpcRequestUtc = DateTimeOffset.UtcNow;
                return new GetResolvedDependenciesResponse
                {
                    Result = result
                };
            }
            catch (OperationCanceledException)
            {
                throw new RpcException(new Status(StatusCode.Cancelled, "Call was cancelled by client."));
            }
            catch (PreprocessorIncludeNotFoundException ex)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"The preprocessor cache could not resolve the include '{ex.SearchValue}'."));
            }
            catch (PreprocessorIdentifierNotDefinedException ex)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"A preprocessor identifier was not defined when evaluating the preprocessor directives: {ex.Message}"));
            }
            catch (PreprocessorResolutionException ex)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"A generic preprocessor resolution exception occurred: {ex}"));
            }
        }
    }
}
