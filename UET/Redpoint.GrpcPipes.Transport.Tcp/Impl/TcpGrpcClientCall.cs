namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System;
    using System.Net;
    using System.Threading.Tasks;

    internal class TcpGrpcClientCall<TRequest, TResponse>
        : IClientStreamWriter<TRequest>
        , IAsyncStreamReader<TResponse>
        where TRequest : class
        where TResponse : class
    {
        private readonly IPEndPoint _endpoint;
        private readonly ILogger? _logger;
        private readonly Method<TRequest, TResponse> _method;
        private readonly CallOptions _options;
        private readonly TRequest? _request;
        private readonly TcpGrpcCallType _callType;
        private readonly Mutex _writeMutex;
        private readonly CancellationTokenSource _cancellationCts;
        private readonly Mutex _requestCancellationMutex;
        private bool _requestCancelled;
        private bool _requestStreamComplete;
        private readonly Semaphore _responseReady;
        private readonly Semaphore _responseHeadersReady;
        private Metadata? _responseHeaders;
        private TResponse? _responseData;
        private Status? _responseStatus;
        private Metadata? _responseTrailers;
        private readonly TerminableAwaitableConcurrentQueue<TResponse> _responseStream;
        private TcpGrpcTransportConnection? _connection;
        private CancellationToken _cancellationToken;
        private CancellationToken _deadlineCancellationToken;
        private WriteOptions? _writeOptions;
        private readonly Gate _callStarted;

        private bool IsStreamingRequestsToServer =>
            _callType == TcpGrpcCallType.ClientStreaming ||
            _callType == TcpGrpcCallType.DuplexStreaming;

        private bool IsStreamingResponsesFromServer =>
            _callType == TcpGrpcCallType.ServerStreaming ||
            _callType == TcpGrpcCallType.DuplexStreaming;

        public TcpGrpcClientCall(
            IPEndPoint endpoint,
            ILogger? logger,
            Method<TRequest, TResponse> method,
            CallOptions options,
            TRequest? request,
            TcpGrpcCallType callType)
        {
            _endpoint = endpoint;
            _logger = logger;
            _method = method;
            _options = options;
            _request = request;
            _callType = callType;
            _writeMutex = new Mutex();
            _cancellationCts = options.Deadline != null
                ? new CancellationTokenSource(_options.Deadline!.Value.AddSeconds(5) - DateTime.UtcNow)
                : new CancellationTokenSource();
            _requestCancellationMutex = new Mutex();
            _requestCancelled = false;
            _responseReady = new Semaphore(0);
            _responseHeadersReady = new Semaphore(0);
            _responseHeaders = null;
            _responseData = null;
            _responseStatus = null;
            _responseTrailers = null;
            _responseStream = new TerminableAwaitableConcurrentQueue<TResponse>();
            _writeOptions = null;
            _callStarted = new Gate();
            _ = Task.Run(ExecuteCallAsync);
        }

        private void LogTrace(string message)
        {
            if (_logger != null)
            {
                _logger.LogTrace($"TcpGrpcAsyncUnaryCall: {message}");
            }
        }

        private async Task ExecuteCallAsync()
        {
            using var deadlineCts = _options.Deadline != null
                ? new CancellationTokenSource(_options.Deadline.Value - DateTime.UtcNow)
                : new CancellationTokenSource();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                _options.CancellationToken,
                deadlineCts.Token);
            _cancellationToken = cts.Token;
            _deadlineCancellationToken = deadlineCts.Token;

            try
            {
                await using ((await TcpGrpcTransportConnection.ConnectAsync(_endpoint, _logger, cts.Token).ConfigureAwait(false))
                    .AsAsyncDisposable(out var connection)
                    .ConfigureAwait(false))
                {
                    try
                    {
                        // Set these so we can send cancellation and perform streaming operations.
                        _connection = connection;

                        // Send the call initialization.
                        LogTrace($"Sending request to call '{_method.FullName}'.");
                        using (await _writeMutex.WaitAsync(cts.Token).ConfigureAwait(false))
                        {
                            await _connection.WriteAsync(new TcpGrpcRequest
                            {
                                FullName = _method.FullName,
                                HasRequestHeaders = _options.Headers != null,
                                RequestHeaders = TcpGrpcMetadataConverter.Convert(_options.Headers),
                                DeadlineUnixTimeMilliseconds = _options.Deadline == null ? 0L : new DateTimeOffset(_options.Deadline.Value, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                            }, cts.Token).ConfigureAwait(false);
                        }
                        cts.Token.ThrowIfCancellationRequested();

                        // Send the request data if requests are non-streaming.
                        if (!IsStreamingRequestsToServer)
                        {
                            LogTrace($"Sending request data to server in non-streaming mode.");
                            using (await _writeMutex.WaitAsync(cts.Token).ConfigureAwait(false))
                            {
                                await _connection.WriteAsync(new TcpGrpcMessage
                                {
                                    Type = TcpGrpcMessageType.RequestData,
                                }, cts.Token).ConfigureAwait(false);
                                var serializationContext = new TcpGrpcSerializationContext();
                                _method.RequestMarshaller.ContextualSerializer(_request!, serializationContext);
                                serializationContext.Complete();
                                await _connection.WriteBlobAsync(serializationContext.Result, cts.Token).ConfigureAwait(false);
                            }
                            cts.Token.ThrowIfCancellationRequested();
                        }
                        else
                        {
                            // Allow the client to start sending streaming requests.
                            _callStarted.Open();
                        }

                        // Read the response messages.
                        LogTrace($"Reading messages from server.");
                        while (true)
                        {
                            TcpGrpcMessage message;
                            try
                            {
                                message = await connection.ReadExpectedAsync<TcpGrpcMessage>(TcpGrpcMessage.Descriptor, cts.Token).ConfigureAwait(false);
                            }
                            catch (RpcException ex)
                            {
                                _responseStatus = ex.Status;
                                _responseTrailers = ex.Trailers;
                                _responseHeadersReady.Release();
                                _responseReady.Release();
                                return;
                            }
                            switch (message.Type)
                            {
                                case TcpGrpcMessageType.ResponseHeaders when _responseHeaders == null && _responseData == null && _responseStatus == null:
                                    {
                                        LogTrace($"Reading response headers from server.");
                                        var headers = await connection.ReadExpectedAsync<TcpGrpcMetadata>(TcpGrpcMetadata.Descriptor, cts.Token).ConfigureAwait(false);
                                        _responseHeaders = TcpGrpcMetadataConverter.Convert(headers);
                                        _responseHeadersReady.Release();
                                        break;
                                    }
                                case TcpGrpcMessageType.ResponseData when _responseData == null || IsStreamingResponsesFromServer:
                                    {
                                        LogTrace($"Reading response data from server.");
                                        using (var memory = await connection.ReadBlobAsync(cts.Token).ConfigureAwait(false))
                                        {
                                            var nextItem = _method.ResponseMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
                                            if (IsStreamingResponsesFromServer)
                                            {
                                                _responseStream.Enqueue(nextItem);
                                            }
                                            else
                                            {
                                                _responseData = nextItem;
                                            }
                                        }
                                        break;
                                    }
                                case TcpGrpcMessageType.ResponseComplete:
                                    {
                                        LogTrace($"Reading response completion from server.");
                                        var complete = await connection.ReadExpectedAsync<TcpGrpcResponseComplete>(TcpGrpcResponseComplete.Descriptor, cts.Token).ConfigureAwait(false);
                                        _responseStatus = new Status((StatusCode)complete.StatusCode, complete.StatusDetails);
                                        if (complete.HasResponseTrailers)
                                        {
                                            _responseTrailers = TcpGrpcMetadataConverter.Convert(complete.ResponseTrailers);
                                        }
                                        _responseHeadersReady.Release();
                                        _responseReady.Release();
                                        _responseStream.Terminate();
                                        return;
                                    }
                                default:
                                    {
                                        LogTrace($"Got unknown message from server.");
                                        _responseStatus = new Status(StatusCode.Internal, "Unexpected message from server for unary call.");
                                        _responseHeadersReady.Release();
                                        _responseReady.Release();
                                        return;
                                    }
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Attempt to send the cancellation request if needed.
                        LogTrace($"OperationCanceledException thrown while handling call.");
                        await HandleClientSideCancelAsync().ConfigureAwait(false);
                        return;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Cancelled during connection.
                if (deadlineCts.IsCancellationRequested)
                {
                    _responseStatus = new Status(StatusCode.DeadlineExceeded, "The server did not respond before the deadline.");
                }
                else
                {
                    _responseStatus = Status.DefaultCancelled;
                }
            }
            catch (Exception ex)
            {
                if (_responseStatus == null)
                {
                    _responseStatus = new Status(StatusCode.Internal, ex.ToString());
                }
            }
            finally
            {
                // We can't send cancellation after this point.
                _connection = null;
                _responseHeadersReady.Release();
                _responseReady.Release();
                _responseStream.Terminate();
                LogTrace($"Call complete via finally.");
            }
        }

        private async Task HandleClientSideCancelAsync()
        {
            await CancelRequestAsync().ConfigureAwait(false);
            if (_deadlineCancellationToken.IsCancellationRequested)
            {
                _responseStatus = new Status(StatusCode.DeadlineExceeded, "The server did not respond before the deadline.");
            }
            else if (!_responseStatus.HasValue || _responseStatus.Value.StatusCode != StatusCode.DeadlineExceeded || _responseStatus.Value.StatusCode != StatusCode.Cancelled)
            {
                _responseStatus = Status.DefaultCancelled;
            }
        }

        private async Task CancelRequestAsync()
        {
            if (_requestCancelled || _connection == null)
            {
                LogTrace($"Ignore CancelRequestAsync because the request has already been cancelled or the connection is unavailable.");
                return;
            }

            try
            {
                LogTrace($"Waiting for request cancellation mutex so we can send client-side cancellation.");
                using (await _requestCancellationMutex.WaitAsync(_cancellationCts.Token).ConfigureAwait(false))
                {
                    if (_requestCancelled || _connection == null)
                    {
                        LogTrace($"Ignore CancelRequestAsync because the request has already been cancelled or the connection is unavailable.");
                        return;
                    }

                    LogTrace($"Waiting for write mutex so we can send client-side cancellation.");
                    using (await _writeMutex.WaitAsync(_cancellationCts.Token).ConfigureAwait(false))
                    {
                        // @note: We can't send RequestCancel if we were interrupted
                        // part way through writing data to the remote stream (such as
                        // during the request send).
                        if (!_connection.HasWriteBeenInterrupted)
                        {
                            LogTrace($"Writing RequestCancel to server.");
                            try
                            {
                                await _connection.WriteAsync(new TcpGrpcMessage
                                {
                                    Type = TcpGrpcMessageType.RequestCancel,
                                }, _cancellationCts.Token).ConfigureAwait(false);
                                LogTrace($"Wrote RequestCancel to server.");
                            }
                            catch (RpcException ex) when (ex.StatusCode == StatusCode.Unavailable)
                            {
                                // The server has completely gone away, so we can't
                                // send cancellation to it.
                                LogTrace($"Unable to write RequestCancel to server, because the server has already disconnected.");
                            }
                        }
                        else
                        {
                            LogTrace($"Write has been interrupted on client-side, so we are unable to send RequestCancel to the server.");
                        }
                        LogTrace($"Marked request as cancelled on client-side.");
                        _requestCancelled = true;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LogTrace($"Unable to send RequestCancel to server because even the extended deadline for sending cancellation has been exceeded.");
            }
        }

        public async Task<TResponse> GetResponseAsync()
        {
            if (IsStreamingResponsesFromServer)
            {
                throw new InvalidOperationException("This call is streaming responses from the server.");
            }

            try
            {
                await _responseReady.WaitAsync(_options.CancellationToken).ConfigureAwait(false);
                if (_responseStatus != null && _responseStatus.Value.StatusCode != StatusCode.OK)
                {
                    throw new RpcException(_responseStatus.Value);
                }
                return _responseData!;
            }
            catch (OperationCanceledException)
            {
                await HandleClientSideCancelAsync().ConfigureAwait(false);
                throw new RpcException(_responseStatus!.Value);
            }
        }

        public async Task<Metadata> GetResponseHeadersAsync()
        {
            try
            {
                await _responseHeadersReady.WaitAsync(_options.CancellationToken).ConfigureAwait(false);
                if (_responseStatus != null && _responseStatus.Value.StatusCode != StatusCode.OK)
                {
                    throw new RpcException(_responseStatus.Value);
                }
                return _responseHeaders ?? new Metadata();
            }
            catch (OperationCanceledException)
            {
                await HandleClientSideCancelAsync().ConfigureAwait(false);
                throw new RpcException(_responseStatus!.Value);
            }
        }

        public Status GetStatus()
        {
            return _responseStatus ?? Status.DefaultCancelled;
        }

        public Metadata GetTrailers()
        {
            return _responseTrailers ?? new Metadata();
        }

        public void Dispose()
        {
            _cancellationCts.Dispose();
        }

        #region IAsyncStreamWriter<TRequest> Implementation

        WriteOptions? IAsyncStreamWriter<TRequest>.WriteOptions
        {
            get => _writeOptions;
            set => _writeOptions = value;
        }

        Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message)
        {
            ArgumentNullException.ThrowIfNull(message);

            return ((IAsyncStreamWriter<TRequest>)this).WriteAsync(message, CancellationToken.None);
        }

        async Task IAsyncStreamWriter<TRequest>.WriteAsync(TRequest message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (_requestStreamComplete)
            {
                throw new InvalidOperationException("You can not call WriteAsync after calling CompleteAsync.");
            }

            if (_cancellationCts == null)
            {
                throw new InvalidOperationException("_cancellationCts is unexpectedly null!");
            }
            if (_callStarted == null)
            {
                throw new InvalidOperationException("_callStarted is unexpectedly null!");
            }

            // We need to have a cancellation token source that is available before
            // the TCP gRPC connection has been set up and _cancellationToken has been set.
            using var beforeConnectionCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationCts.Token,
                cancellationToken);

            // Wait until the background task has got the connection set up and
            // ready to write requests.
            await _callStarted.WaitAsync(beforeConnectionCts.Token).ConfigureAwait(false);

            // Now that we're ready to send requests, we need a cancellation token source
            // that is using the _cancellationToken set up after the connection has been
            // established.
            using var afterConnectionCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken,
                cancellationToken);

            if (_responseStatus != null && _responseStatus.Value.StatusCode != StatusCode.OK)
            {
                if (_responseStatus.Value.StatusCode == StatusCode.Cancelled)
                {
                    // Normalize the cancellation as OperationCanceledException.
                    throw new OperationCanceledException();
                }

                throw new RpcException(_responseStatus.Value);
            }

            ObjectDisposedException.ThrowIf(_connection == null, this);

            if (IsStreamingRequestsToServer)
            {
                LogTrace($"Sending request data to server in streaming mode.");
                if (_writeMutex == null)
                {
                    throw new InvalidOperationException("_writeMutex is unexpectedly null!");
                }
                using (await _writeMutex.WaitAsync(afterConnectionCts.Token).ConfigureAwait(false))
                {
                    if (_connection == null)
                    {
                        throw new InvalidOperationException("_connection is unexpectedly null!");
                    }
                    await _connection.WriteAsync(new TcpGrpcMessage
                    {
                        Type = TcpGrpcMessageType.RequestData,
                    }, afterConnectionCts.Token).ConfigureAwait(false);
                    if (_method == null)
                    {
                        throw new InvalidOperationException("_method is unexpectedly null!");
                    }
                    if (_method.RequestMarshaller == null)
                    {
                        throw new InvalidOperationException("_method.RequestMarshaller is unexpectedly null!");
                    }
                    if (_method.RequestMarshaller.ContextualSerializer == null)
                    {
                        throw new InvalidOperationException("_method.RequestMarshaller.ContextualSerializer is unexpectedly null!");
                    }
                    var serializationContext = new TcpGrpcSerializationContext();
                    _method.RequestMarshaller.ContextualSerializer(message, serializationContext);
                    serializationContext.Complete();
                    await _connection.WriteBlobAsync(serializationContext.Result, afterConnectionCts.Token).ConfigureAwait(false);
                }
                afterConnectionCts.Token.ThrowIfCancellationRequested();
            }
            else
            {
                throw new NotSupportedException("This call is not streaming requests to the server.");
            }
        }

        async Task IClientStreamWriter<TRequest>.CompleteAsync()
        {
            // Wait until the background task has got the connection set up and
            // ready to complete the operation.
            await _callStarted.WaitAsync(_cancellationCts.Token).ConfigureAwait(false);

            if (_responseStatus != null && _responseStatus.Value.StatusCode != StatusCode.OK)
            {
                if (_responseStatus.Value.StatusCode == StatusCode.Cancelled)
                {
                    // Normalize the cancellation as OperationCanceledException.
                    throw new OperationCanceledException();
                }

                throw new RpcException(_responseStatus.Value);
            }

            ObjectDisposedException.ThrowIf(_connection == null, this);

            if (IsStreamingRequestsToServer)
            {
                LogTrace($"Sending completion request to server in streaming mode.");
                using (await _writeMutex.WaitAsync(_cancellationToken).ConfigureAwait(false))
                {
                    await _connection.WriteAsync(new TcpGrpcMessage
                    {
                        Type = TcpGrpcMessageType.RequestComplete,
                    }, _cancellationToken).ConfigureAwait(false);
                }
                _requestStreamComplete = true;
                _cancellationToken.ThrowIfCancellationRequested();
            }
            else
            {
                throw new NotSupportedException("This call is not streaming requests to the server.");
            }
        }

        #endregion

        #region IAsyncStreamReader<TResponse>

        TResponse IAsyncStreamReader<TResponse>.Current
        {
            get
            {
                if (!IsStreamingResponsesFromServer)
                {
                    throw new NotSupportedException("This call is not streaming responses from the server.");
                }

                if (_responseData == null)
                {
                    throw new InvalidOperationException("Call MoveNext first before attempting to access the Current response.");
                }

                return _responseData;
            }
        }

        async Task<bool> IAsyncStreamReader<TResponse>.MoveNext(CancellationToken cancellationToken)
        {
            if (!IsStreamingResponsesFromServer)
            {
                throw new NotSupportedException("This call is not streaming responses from the server.");
            }

            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationToken, cancellationToken);
                var (nextItem, terminated) = await _responseStream.TryDequeueAsync(cts.Token).ConfigureAwait(false);
                if (terminated)
                {
                    if (_responseStatus != null && _responseStatus.Value.StatusCode != StatusCode.OK)
                    {
                        throw new RpcException(_responseStatus.Value);
                    }
                    return false;
                }
                _responseData = nextItem;
                return true;
            }
            catch (OperationCanceledException) when (_deadlineCancellationToken.IsCancellationRequested)
            {
                throw new RpcException(new Status(StatusCode.DeadlineExceeded, "The server did not respond before the deadline."));
            }
        }

        #endregion
    }
}
