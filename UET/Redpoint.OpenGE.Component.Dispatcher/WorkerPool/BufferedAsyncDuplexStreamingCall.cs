namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System.Runtime.ExceptionServices;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    public class BufferedAsyncDuplexStreamingCall<TRequest, TResponse> : IAsyncEnumerable<TResponse>, IAsyncDisposable where TResponse : class
    {
        private readonly ILogger _logger;
        private readonly AsyncDuplexStreamingCall<TRequest, TResponse> _call;
        private Task? _bgTask;
        private readonly TerminableAwaitableConcurrentQueue<(TResponse? response, ExceptionDispatchInfo? ex)> _queue;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private ExceptionDispatchInfo? _exception;

        public BufferedAsyncDuplexStreamingCall(
            ILogger logger,
            AsyncDuplexStreamingCall<TRequest, TResponse> underlyingCall)
        {
            _logger = logger;
            _call = underlyingCall;
            _queue = new TerminableAwaitableConcurrentQueue<(TResponse? response, ExceptionDispatchInfo? ex)>();
            _cancellationTokenSource = new CancellationTokenSource();
            _exception = null;
        }

        public void StartObserving()
        {
            if (_bgTask == null)
            {
                _bgTask = Task.Run(ProcessResponseStreamAsync);
            }
        }

        public Exception? Exception => _exception?.SourceException;

        public Func<Exception, Task>? OnException { get; set; }

        public IClientStreamWriter<TRequest> RequestStream => _call.RequestStream;

        private async Task ProcessResponseStreamAsync()
        {
            _logger.LogTrace("Starting observation of AsyncDuplexStreamingCall");
            try
            {
                while (await _call.ResponseStream.MoveNext(_cancellationTokenSource.Token))
                {
                    var current = _call.ResponseStream.Current;
                    _logger.LogTrace($"Enqueuing AsyncDuplexStreamingCall: {current}");
                    _queue.Enqueue((current, null));
                }
                _logger.LogTrace($"Terminating AsyncDuplexStreamingCall due to normal completion");
                _queue.Terminate();
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                _logger.LogTrace($"Terminating AsyncDuplexStreamingCall due to call being called cancelled by client");
                _queue.Terminate();
            }
            catch (OperationCanceledException) when (_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogTrace($"Terminating AsyncDuplexStreamingCall due to cancellation token");
                _queue.Terminate();
            }
            catch (Exception ex)
            {
                _logger.LogTrace($"AsyncDuplexStreamingCall got exception: {ex}");
                _exception = ExceptionDispatchInfo.Capture(ex);
                OnException?.Invoke(ex);
                _queue.Enqueue((null, _exception));
                _queue.Terminate();
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

        public ValueTask DisposeAsync()
        {
            if (!_cancellationTokenSource.IsCancellationRequested)
            {
                _logger.LogTrace($"AsyncDuplexStreamingCall being disposed");
                _cancellationTokenSource.Cancel();
            }
            return ValueTask.CompletedTask;
        }
    }
}
