namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using global::Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System;
    using System.Net;
    using System.Numerics;
    using System.Threading.Tasks;

    internal class TcpGrpcAsyncUnaryCall<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        private readonly IPEndPoint _endpoint;
        private readonly ILogger? _logger;
        private readonly Method<TRequest, TResponse> _method;
        private readonly CallOptions _options;
        private readonly TRequest _request;
        private readonly Mutex _writeMutex;
        private readonly CancellationTokenSource _cancellationCts;
        private readonly Mutex _requestCancellationMutex;
        private bool _requestCancelled;
        private readonly Semaphore _responseReady;
        private readonly Semaphore _responseHeadersReady;
        private Metadata? _responseHeaders;
        private TResponse? _responseData;
        private Status? _responseStatus;
        private Metadata? _responseTrailers;
        private TcpGrpcTransportConnection? _connection;

        public TcpGrpcAsyncUnaryCall(
            IPEndPoint endpoint,
            ILogger? logger,
            Method<TRequest, TResponse> method,
            CallOptions options,
            TRequest request)
        {
            _endpoint = endpoint;
            _logger = logger;
            _method = method;
            _options = options;
            _request = request;
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

            try
            {
                await using ((await TcpGrpcTransportConnection.ConnectAsync(_endpoint, _logger, cts.Token).ConfigureAwait(false))
                    .AsAsyncDisposable(out var connection)
                    .ConfigureAwait(false))
                {
                    try
                    {
                        // Set this so we can send cancellation.
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

                        // Send the request data.
                        LogTrace($"Sending request data.");
                        using (await _writeMutex.WaitAsync(cts.Token).ConfigureAwait(false))
                        {
                            await _connection.WriteAsync(new TcpGrpcMessage
                            {
                                Type = TcpGrpcMessageType.RequestData,
                            }, cts.Token).ConfigureAwait(false);
                            var serializationContext = new TcpGrpcSerializationContext();
                            _method.RequestMarshaller.ContextualSerializer(_request, serializationContext);
                            serializationContext.Complete();
                            await _connection.WriteBlobAsync(serializationContext.Result, cts.Token).ConfigureAwait(false);
                        }
                        cts.Token.ThrowIfCancellationRequested();

                        // Read the response messages.
                        LogTrace($"Reading messages from server.");
                        while (true)
                        {
                            TcpGrpcMessage message;
                            try
                            {
                                message = await _connection.ReadExpectedAsync<TcpGrpcMessage>(TcpGrpcMessage.Descriptor, cts.Token).ConfigureAwait(false);
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
                                        var headers = await _connection.ReadExpectedAsync<TcpGrpcMetadata>(TcpGrpcMetadata.Descriptor, cts.Token).ConfigureAwait(false);
                                        _responseHeaders = TcpGrpcMetadataConverter.Convert(headers);
                                        _responseHeadersReady.Release();
                                        break;
                                    }
                                case TcpGrpcMessageType.ResponseData when _responseData == null:
                                    {
                                        LogTrace($"Reading response data from server.");
                                        using (var memory = await _connection.ReadBlobAsync(cts.Token).ConfigureAwait(false))
                                        {
                                            _responseData = _method.ResponseMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
                                        }
                                        break;
                                    }
                                case TcpGrpcMessageType.ResponseComplete:
                                    {
                                        LogTrace($"Reading response completion from server.");
                                        var complete = await _connection.ReadExpectedAsync<TcpGrpcResponseComplete>(TcpGrpcResponseComplete.Descriptor, cts.Token).ConfigureAwait(false);
                                        _responseStatus = new Status((StatusCode)complete.StatusCode, complete.StatusDetails);
                                        if (complete.HasResponseTrailers)
                                        {
                                            _responseTrailers = TcpGrpcMetadataConverter.Convert(complete.ResponseTrailers);
                                        }
                                        _responseHeadersReady.Release();
                                        _responseReady.Release();
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
                if (_options.Deadline != null &&
                    _options.Deadline < DateTimeOffset.UtcNow)
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
                LogTrace($"Call complete via finally.");
            }
        }

        private async Task HandleClientSideCancelAsync()
        {
            await CancelRequestAsync().ConfigureAwait(false);
            if (_options.Deadline != null &&
                _options.Deadline < DateTimeOffset.UtcNow)
            {
                _responseStatus = new Status(StatusCode.DeadlineExceeded, "The server did not respond before the deadline.");
            }
            else
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
    }
}
