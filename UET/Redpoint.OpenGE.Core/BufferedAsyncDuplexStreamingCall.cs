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
        private readonly string _uniqueAssignmentId;
        private Task? _bgTask;
        private readonly TerminableAwaitableConcurrentQueue<(TResponse? response, ExceptionDispatchInfo? ex)> _queue;
        private ExceptionDispatchInfo? _exception;
        private AsyncEvent<Exception> _onException;
        private AsyncEvent<StatusCode> _onTerminated;

        public BufferedAsyncDuplexStreamingCall(
            ILogger logger,
            ITaskScheduler taskScheduler,
            AsyncDuplexStreamingCall<TRequest, TResponse> underlyingCall,
            string uniqueAssignmentId)
        {
            if (taskScheduler == null) throw new ArgumentNullException(nameof(taskScheduler));

            _logger = logger;
            _taskSchedulerScope = taskScheduler.CreateSchedulerScope($"BufferedAsyncDuplexStreamingCall/{uniqueAssignmentId}", CancellationToken.None);
            _call = underlyingCall;
            _uniqueAssignmentId = uniqueAssignmentId;
            _queue = new TerminableAwaitableConcurrentQueue<(TResponse? response, ExceptionDispatchInfo? ex)>();
            _exception = null;
            _onException = new AsyncEvent<Exception>();
            _onTerminated = new AsyncEvent<StatusCode>();
        }

        public void StartObserving()
        {
            if (_bgTask == null)
            {
                _bgTask = _taskSchedulerScope.RunAsync("ProcessResponseStream", ProcessResponseStreamAsync, CancellationToken.None);
            }
        }

        public Exception? Exception => _exception?.SourceException;

        public IAsyncEvent<Exception> OnException => _onException;

        public IAsyncEvent<StatusCode> OnTerminated => _onTerminated;

        public IClientStreamWriter<TRequest> RequestStream => _call.RequestStream;

        private async Task ProcessResponseStreamAsync(CancellationToken cancellationToken)
        {
            _logger.LogTrace($"{_uniqueAssignmentId}: Starting observation of AsyncDuplexStreamingCall");
            try
            {
                while (await _call.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false))
                {
                    var current = _call.ResponseStream.Current;
                    _logger.LogTrace($"{_uniqueAssignmentId}: Enqueuing AsyncDuplexStreamingCall: {current}");
                    _queue.Enqueue((current, null));
                }
                _logger.LogTrace($"{_uniqueAssignmentId}: Terminating AsyncDuplexStreamingCall due to normal completion");
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.OK, CancellationToken.None).ConfigureAwait(false);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: Terminating AsyncDuplexStreamingCall due to call being called cancelled by client");
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.Cancelled, CancellationToken.None).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: Terminating AsyncDuplexStreamingCall due to cancellation token");
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.Cancelled, CancellationToken.None).ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall got exception: {ex}");
                _exception = ExceptionDispatchInfo.Capture(ex);
                await _onException.BroadcastAsync(ex, CancellationToken.None).ConfigureAwait(false);
                _queue.Enqueue((null, _exception));
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(ex.StatusCode, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall got exception: {ex}");
                _exception = ExceptionDispatchInfo.Capture(ex);
                await _onException.BroadcastAsync(ex, CancellationToken.None).ConfigureAwait(false);
                _queue.Enqueue((null, _exception));
                _queue.Terminate();
                await _onTerminated.BroadcastAsync(StatusCode.Internal, CancellationToken.None).ConfigureAwait(false);
            }
        }

        public async ValueTask<TResponse> GetNextAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall/GetNextAsync attempting to DequeueAsync");
            var result = await _queue.DequeueAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall/GetNextAsync finished DequeueAsync call");
            if (result.response != null)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall/GetNextAsync returning response");
                return result.response;
            }
            else if (result.ex != null)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall/GetNextAsync throwing exception: {result.ex.SourceException}");
                result.ex.Throw();
                throw new InvalidProgramException("Expected result.ex.Throw() to throw an exception before this point.");
            }
            else
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall/GetNextAsync throwing OperationCanceledException");
                throw new OperationCanceledException("The underlying response stream has been closed normally.");
            }
        }

        public async IAsyncEnumerator<TResponse> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall enumeration starting");
            await foreach (var element in _queue.WithCancellation(cancellationToken))
            {
                if (element.response != null)
                {
                    _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall yielding value: {element.response}");
                    yield return element.response;
                }
                else
                {
                    // Exception, which we'll check after exiting the loop anyway.
                    _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall detected non-response, breaking loop.");
                    break;
                }
            }
            if (_exception != null)
            {
                _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall throwing exception: {_exception.SourceException}");
                _exception.Throw();
            }
            _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall enumeration ending");
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall being disposed");
            await _taskSchedulerScope.DisposeAsync().ConfigureAwait(false);
            _logger.LogTrace($"{_uniqueAssignmentId}: AsyncDuplexStreamingCall was disposed");
        }
    }
}
