namespace Redpoint.Grpc.Transport.Tcp
{
    using global::Grpc.Core;
    using Redpoint.Concurrency;
    using System;
    using System.Net;
    using System.Threading.Tasks;

    internal class TcpGrpcAsyncUnaryCall<TRequest, TResponse>
        where TRequest : class
        where TResponse : class
    {
        private readonly IPEndPoint _endpoint;
        private readonly Method<TRequest, TResponse> _method;
        private readonly CallOptions _options;
        private readonly TRequest _request;
        private readonly Mutex _writeMutex;
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
            Method<TRequest, TResponse> method,
            CallOptions options,
            TRequest request)
        {
            _endpoint = endpoint;
            _method = method;
            _options = options;
            _request = request;
            _writeMutex = new Mutex();
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

        private async Task ExecuteCallAsync()
        {
            try
            {
                await using ((await TcpGrpcTransportConnection.ConnectAsync(_endpoint, _options.CancellationToken).ConfigureAwait(false))
                    .AsAsyncDisposable(out var connection)
                    .ConfigureAwait(false))
                {
                    // Set this so we can send cancellation.
                    _connection = connection;

                    // Send the call initialization.
                    using (await _writeMutex.WaitAsync(_options.CancellationToken).ConfigureAwait(false))
                    {
                        await _connection.WriteAsync(new TcpGrpcRequest
                        {
                            FullName = _method.FullName,
                            HasRequestHeaders = _options.Headers != null,
                            RequestHeaders = TcpGrpcMetadataConverter.Convert(_options.Headers),
                            DeadlineUnixTimeMilliseconds = _options.Deadline == null ? 0L : new DateTimeOffset(_options.Deadline.Value, TimeSpan.Zero).ToUnixTimeMilliseconds(),
                        }).ConfigureAwait(false);
                    }
                    _options.CancellationToken.ThrowIfCancellationRequested();

                    // Send the request data.
                    using (await _writeMutex.WaitAsync(_options.CancellationToken).ConfigureAwait(false))
                    {
                        await _connection.WriteAsync(new TcpGrpcMessage
                        {
                            Type = TcpGrpcMessageType.RequestData,
                        }).ConfigureAwait(false);
                        var serializationContext = new TcpGrpcSerializationContext();
                        _method.RequestMarshaller.ContextualSerializer(_request, serializationContext);
                        serializationContext.Complete();
                        await _connection.WriteBlobAsync(serializationContext.Result).ConfigureAwait(false);
                    }
                    _options.CancellationToken.ThrowIfCancellationRequested();

                    // Read the response messages.
                    while (true)
                    {
                        var message = await _connection.ReadExpectedAsync<TcpGrpcMessage>(TcpGrpcMessage.Descriptor, _options.CancellationToken).ConfigureAwait(false);
                        switch (message.Type)
                        {
                            case TcpGrpcMessageType.ResponseHeaders when _responseHeaders == null && _responseData == null && _responseStatus == null:
                                {
                                    var headers = await _connection.ReadExpectedAsync<TcpGrpcMetadata>(TcpGrpcMetadata.Descriptor, _options.CancellationToken).ConfigureAwait(false);
                                    _responseHeaders = TcpGrpcMetadataConverter.Convert(headers);
                                    _responseHeadersReady.Release();
                                    break;
                                }
                            case TcpGrpcMessageType.ResponseData when _responseData == null:
                                {
                                    using (var memory = await _connection.ReadBlobAsync(_options.CancellationToken).ConfigureAwait(false))
                                    {
                                        _responseData = _method.ResponseMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
                                    }
                                    break;
                                }
                            case TcpGrpcMessageType.ResponseComplete:
                                {
                                    var complete = await _connection.ReadExpectedAsync<TcpGrpcResponseComplete>(TcpGrpcResponseComplete.Descriptor, _options.CancellationToken).ConfigureAwait(false);
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
                                    _responseStatus = new Status(StatusCode.Internal, "Unexpected message from server for unary call.");
                                    _responseHeadersReady.Release();
                                    _responseReady.Release();
                                    return;
                                }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Attempt to send the cancellation request if needed.
                await CancelRequestAsync().ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                if (_responseStatus == null)
                {
                    _responseStatus = new Status(StatusCode.Internal, ex.ToString());
                }
                throw;
            }
            finally
            {
                // We can't send cancellation after this point.
                _connection = null;
                _responseHeadersReady.Release();
                _responseReady.Release();
            }
        }

        private async Task CancelRequestAsync()
        {
            if (_requestCancelled || _connection == null)
            {
                return;
            }

            using (await _requestCancellationMutex.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (_requestCancelled || _connection == null)
                {
                    return;
                }

                using (await _writeMutex.WaitAsync(_options.CancellationToken).ConfigureAwait(false))
                {
                    await _connection.WriteAsync(new TcpGrpcMessage
                    {
                        Type = TcpGrpcMessageType.RequestCancel,
                    }).ConfigureAwait(false);
                    _requestCancelled = true;
                }
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
                await CancelRequestAsync().ConfigureAwait(false);
                _responseStatus = Status.DefaultCancelled;
                throw new RpcException(_responseStatus.Value);
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
                await CancelRequestAsync().ConfigureAwait(false);
                _responseStatus = Status.DefaultCancelled;
                throw new RpcException(_responseStatus.Value);
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

#pragma warning disable CA1822
        public void Dispose()
#pragma warning restore CA1822
        {
            // @todo: Should we do something here?
        }
    }
}
