namespace Redpoint.OpenGE.Core
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.Tasks;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;

    public class BufferedAsyncDuplexStreamingCall<TRequest, TResponse> : IAsyncEnumerable<TResponse>, IAsyncDisposable where TResponse : class
    {
        private readonly ILogger _logger;
        private readonly ITaskSchedulerScope _taskSchedulerScope;
        private readonly AsyncDuplexStreamingCall<TRequest, TResponse> _call;
        private Task? _bgTask;
        private readonly TerminableAwaitableConcurrentQueue<(TResponse? response, ExceptionDispatchInfo? ex)> _queue;
        private ExceptionDispatchInfo? _exception;
        private AsyncEvent<Exception> _onException;
        private AsyncEvent<StatusCode> _onTerminated;

        public BufferedAsyncDuplexStreamingCall(
            ILogger logger,
            ITaskScheduler taskScheduler,
            AsyncDuplexStreamingCall<TRequest, TResponse> underlyingCall)
        {
            _logger = logger;
            _taskSchedulerScope = taskScheduler.CreateSchedulerScope("BufferedAsyncDuplexStreamingCall", CancellationToken.None);
            _call = underlyingCall;
            _queue = new TerminableAwaitableConcurrentQueue<(TResponse? response, ExceptionDispatchInfo? ex)>();
            _exception = null;
            _onException = new AsyncEvent<Exception>();
            _onTerminated = new AsyncEvent<StatusCode>();
        }

        public void StartObserving()
        {
            if (_bgTask == null)
            {
                _bgTask = _taskSchedulerScope.RunAsync("ProcessResponseStream", CancellationToken.None, ProcessResponseStreamAsync);
            }
        }

        public Exception? Exception => _exception?.SourceException;

        public IAsyncEvent<Exception> OnException => _onException;

        public IAsyncEvent<StatusCode> OnTerminated => _onTerminated;

        public IClientStreamWriter<TRequest> RequestStream => _call.RequestStream;

        private async Task ProcessResponseStreamAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace("Starting observation of AsyncDuplexStreamingCall");
            try
            {
                while (await _call.ResponseStream.MoveNext(cancellationToken))
                {
                    var current = _call.ResponseStream.Current;
                    _logger.LogTrace($"Enqueuing AsyncDuplexStreamingCall: {current}");
                    _queue.Enqueue((current, null));
                }
                _logger.LogTrace($"Terminating AsyncDuplexStreamingCall due to normal completion");
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.OK, CancellationToken.None);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogTrace($"Terminating AsyncDuplexStreamingCall due to call being called cancelled by client");
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.Cancelled, CancellationToken.None);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogTrace($"Terminating AsyncDuplexStreamingCall due to cancellation token");
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.Cancelled, CancellationToken.None);
            }
            catch (RpcException ex)
            {
                _logger.LogTrace($"AsyncDuplexStreamingCall got exception: {ex}");
                _exception = ExceptionDispatchInfo.Capture(ex);
                await _onException.BroadcastAsync(ex, CancellationToken.None);
                _queue.Enqueue((null, _exception));
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(ex.StatusCode, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"AsyncDuplexStreamingCall got exception: {ex}");
                _exception = ExceptionDispatchInfo.Capture(ex);
                await _onException.BroadcastAsync(ex, CancellationToken.None);
                _queue.Enqueue((null, _exception));
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.Internal, CancellationToken.None);
            }
        }

        public async ValueTask<TResponse> GetNextAsync(CancellationToken cancellationToken = default)
        {
            var result = await _queue.DequeueAsync(cancellationToken);
            if (result.response != null)
            {
                return result.response;
            }
            else if (result.ex != null)
            {
                result.ex.Throw();
                throw new InvalidProgramException("Expected result.ex.Throw() to throw an exception before this point.");
            }
            else
            {
                throw new OperationCanceledException("The underlying response stream has been closed normally.");
            }
        }

        public async IAsyncEnumerator<TResponse> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            await foreach (var element in _queue.WithCancellation(cancellationToken))
            {
                if (element.response != null)
                {
                    _logger.LogTrace($"AsyncDuplexStreamingCall yielding value: {element.response}");
                    yield return element.response;
                }
                else
                {
                    // Exception, which we'll check after exiting the loop anyway.
                    break;
                }
            }
            if (_exception != null)
            {
                _logger.LogTrace($"AsyncDuplexStreamingCall throwing exception: {_exception.SourceException}");
                _exception.Throw();
            }
            _logger.LogTrace($"AsyncDuplexStreamingCall enumeration ending");
        }

        public async ValueTask DisposeAsync()
        {
            _logger.LogTrace($"AsyncDuplexStreamingCall being disposed");
            await _taskSchedulerScope.DisposeAsync();
        }
    }
}
