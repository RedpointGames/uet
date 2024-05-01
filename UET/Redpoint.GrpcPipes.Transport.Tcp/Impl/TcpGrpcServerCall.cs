namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using Grpc.Core;
    using Redpoint.Concurrency;
    using System;
    using System.Data.Common;
    using System.Threading;

    internal class TcpGrpcServerCall<TRequest, TResponse>
        : IServerStreamWriter<TResponse>
        , IAsyncStreamReader<TRequest>
        where TRequest : class
        where TResponse : class
    {
        private readonly TcpGrpcServerIncomingCall _incoming;
        private readonly TcpGrpcServerCallContext _serverCallContext;
        private readonly Method<TRequest, TResponse> _method;
        private readonly TcpGrpcCallType _callType;
        private WriteOptions? _writeOptions;
        private TRequest? _requestData;
        private readonly TerminableAwaitableConcurrentQueue<TRequest> _requestStream;

        private bool IsStreamingRequestsFromClient =>
            _callType == TcpGrpcCallType.ClientStreaming ||
            _callType == TcpGrpcCallType.DuplexStreaming;

        private bool IsStreamingResponsesToClient =>
            _callType == TcpGrpcCallType.ServerStreaming ||
            _callType == TcpGrpcCallType.DuplexStreaming;

        public TcpGrpcServerCall(
            TcpGrpcServerIncomingCall incoming,
            TcpGrpcServerCallContext serverCallContext,
            Method<TRequest, TResponse> method,
            TcpGrpcCallType callType)
        {
            _incoming = incoming;
            _serverCallContext = serverCallContext;
            _method = method;
            _callType = callType;
            _writeOptions = null;
            _requestData = null;
            _requestStream = new TerminableAwaitableConcurrentQueue<TRequest>();
        }

        private async Task SendStatusAsync(StatusCode statusCode, string details)
        {
            try
            {
                _incoming.LogTrace($"Waiting for write lock to send status ({statusCode}, '{details}') to client.");
                using (await _serverCallContext.WriteMutex.WaitAsync(_serverCallContext.DeadlineCancellationToken).ConfigureAwait(false))
                {
                    _incoming.LogTrace($"Writing of status ({statusCode}, '{details}') to client.");
                    await _incoming.Connection.WriteAsync(new TcpGrpcMessage
                    {
                        Type = TcpGrpcMessageType.ResponseComplete,
                    }, _serverCallContext.DeadlineCancellationToken).ConfigureAwait(false);
                    await _incoming.Connection.WriteAsync(new TcpGrpcResponseComplete
                    {
                        StatusCode = (int)statusCode,
                        StatusDetails = details,
                        HasResponseTrailers = _serverCallContext.ResponseTrailers.Count > 0,
                        ResponseTrailers = TcpGrpcMetadataConverter.Convert(_serverCallContext.ResponseTrailers),
                    }, _serverCallContext.DeadlineCancellationToken).ConfigureAwait(false);
                    _incoming.LogTrace($"Wrote status ({statusCode}, '{details}') to client.");
                }
            }
            catch (OperationCanceledException) when (_serverCallContext.DeadlineCancellationToken.IsCancellationRequested)
            {
                // We can't send any content to the client, because we have exceeded our extended deadline cancellation.
                _incoming.LogTrace($"Unable to send ({statusCode}, '{details}') to client because the call has already exceeded the extended deadline cancellation.");
                return;
            }
        }

        /// <summary>
        /// Monitors the client stream for cancellation and (if the request is streaming),
        /// streaming requests that the client sends.
        /// </summary>
        /// <param name="stopReceiving">A cancellation token source that is used to stop receiving data from the client.</param>
        private async Task MonitorClientCancellationAndStreamingRequestsAsync(
            CancellationTokenSource stopReceiving)
        {
            // Read the next message to see if we get cancelled.
            while (!stopReceiving.IsCancellationRequested && !_incoming.Connection.HasReadBeenInterrupted)
            {
                TcpGrpcMessage message;
                try
                {
                    message = await _incoming.Connection.ReadExpectedAsync<TcpGrpcMessage>(
                        TcpGrpcMessage.Descriptor,
                        stopReceiving.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // The invocation completed and calls were stopped.
                    return;
                }
                catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Unavailable)
                {
                    // The client closed the connection. 
                    _incoming.LogTrace($"Server is cancelling the operation as the client closed the connection.");
                    _serverCallContext.CancellationTokenSource.Cancel();
                    return;
                }

                switch (message.Type)
                {
                    case TcpGrpcMessageType.RequestCancel:
                        // We don't need to send cancellation here; if it's relevant it'll
                        // be handled in invokeTask.
                        _incoming.LogTrace($"Server received cancellation from client.");
                        _serverCallContext.CancellationTokenSource.Cancel();
                        return;
                    case TcpGrpcMessageType.RequestComplete when IsStreamingRequestsFromClient:
                        // The client has finished sending requests.
                        _requestStream.Terminate();
                        break;
                    case TcpGrpcMessageType.RequestData when IsStreamingRequestsFromClient:
                        // We're receiving streamed request data from the client.
                        {
                            _incoming.LogTrace($"Reading request data from client in streaming mode.");
                            using var memory = await _incoming.Connection.ReadBlobAsync(_serverCallContext.CancellationToken).ConfigureAwait(false);
                            var nextItem = _method.RequestMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
                            _requestStream.Enqueue(nextItem);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Attempt to read the request data for a non-streaming client request. If this returns null,
        /// the caller should immediately return as the client cancelled the request or misbehaved.
        /// </summary>
        public async Task<TRequest?> TryReadNonStreamingClientRequestDataAsync()
        {
            if (IsStreamingRequestsFromClient)
            {
                throw new InvalidOperationException("This call is streaming requests from the client.");
            }

            // Read the message type (it must be RequestData).
            _incoming.LogTrace($"Reading next message type from client.");
            var message = await _incoming.Connection.ReadExpectedAsync<TcpGrpcMessage>(
                TcpGrpcMessage.Descriptor,
                _serverCallContext.CancellationToken).ConfigureAwait(false);
            if (message.Type == TcpGrpcMessageType.RequestCancel)
            {
                // The client cancelled the request, so we don't need to send
                // a status to them.
                _serverCallContext.CancellationTokenSource.Cancel();
                return null;
            }
            if (message.Type != TcpGrpcMessageType.RequestData)
            {
                await SendStatusAsync(
                    StatusCode.Internal,
                    "Client did not send request data.").ConfigureAwait(false);
                return null;
            }

            // Read the request data.
            _incoming.LogTrace($"Reading request data from client in non-streaming mode.");
            using var memory = await _incoming.Connection.ReadBlobAsync(_serverCallContext.CancellationToken).ConfigureAwait(false);
            return _method.RequestMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
        }

        public async Task InvokeHandlerWithClientMonitoringAsync(
            Func<Task<TResponse?>> serverInvocation)
        {
            using var stopReceiving = CancellationTokenSource.CreateLinkedTokenSource(_serverCallContext.CancellationToken);
            var receiveFromClientTask = Task.Run(async () =>
            {
                // Monitor incoming stream for cancellation or streamed requests.
                await MonitorClientCancellationAndStreamingRequestsAsync(stopReceiving)
                    .ConfigureAwait(false);
            });
            var processOnServerTask = Task.Run(async () =>
            {
                // Invoke the handler.
                TResponse? response;
                try
                {
                    _incoming.LogTrace($"Invoking server-side implementation of method.");
                    response = await serverInvocation().ConfigureAwait(false);
                }
                catch (RpcException ex)
                {
                    _incoming.LogTrace($"Server-side method implementation threw RpcException.");
                    await SendStatusAsync(
                        ex.Status.StatusCode,
                        ex.Status.Detail).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    _incoming.LogTrace($"Server-side method implementation threw OperationCanceledException.");
                    await SendStatusAsync(
                        Status.DefaultCancelled.StatusCode,
                        Status.DefaultCancelled.Detail).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    _incoming.LogTrace($"Server-side method implementation threw an unknown exception: {ex}");
                    await SendStatusAsync(
                        StatusCode.Unknown,
                        ex.ToString()).ConfigureAwait(false);
                    return;
                }

                // Ensure the response value is correct for the streaming mode.
                if (response == null && !IsStreamingResponsesToClient)
                {
                    _incoming.LogTrace($"Server-side method implementation did not return a response in non-streaming mode. This is an internal error.");
                    await SendStatusAsync(
                        StatusCode.Unknown,
                        "The server encountered an internal error.").ConfigureAwait(false);
                    return;
                }
                if (response != null && IsStreamingResponsesToClient)
                {
                    _incoming.LogTrace($"Server-side method implementation returned a response value in streaming mode. This is an internal error.");
                    await SendStatusAsync(
                        StatusCode.Unknown,
                        "The server encountered an internal error.").ConfigureAwait(false);
                    return;
                }

                // The call is complete.
                try
                {
                    if (!IsStreamingResponsesToClient)
                    {
                        _incoming.LogTrace($"Waiting for write lock to write response to client.");
                        using (await _serverCallContext.WriteMutex.WaitAsync(_serverCallContext.CancellationToken).ConfigureAwait(false))
                        {
                            _incoming.LogTrace($"Writing response to client.");
                            await _incoming.Connection.WriteAsync(new TcpGrpcMessage
                            {
                                Type = TcpGrpcMessageType.ResponseData,
                            }, _serverCallContext.CancellationToken).ConfigureAwait(false);
                            var serializationContext = new TcpGrpcSerializationContext();
                            _method.ResponseMarshaller.ContextualSerializer(
                                response!,
                                serializationContext);
                            serializationContext.Complete();
                            await _incoming.Connection.WriteBlobAsync(
                                serializationContext.Result,
                                _serverCallContext.CancellationToken)
                                .ConfigureAwait(false);
                            _incoming.LogTrace($"Wrote response to client.");
                        }
                    }
                    await SendStatusAsync(
                        Status.DefaultSuccess.StatusCode,
                        Status.DefaultSuccess.Detail).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    _incoming.LogTrace($"Server handling was cancelled before response could be entirely written.");
                }
                return;
            });
            await Task.WhenAny(receiveFromClientTask, processOnServerTask).ConfigureAwait(false);
            stopReceiving.Cancel();
            await Task.WhenAll(receiveFromClientTask, processOnServerTask).ConfigureAwait(false);
        }

        #region IServerStreamWriter<TResponse> Implementation

        WriteOptions? IAsyncStreamWriter<TResponse>.WriteOptions
        {
            get => _writeOptions;
            set => _writeOptions = value;
        }

        Task IAsyncStreamWriter<TResponse>.WriteAsync(TResponse message)
        {
            ArgumentNullException.ThrowIfNull(message);

            return ((IAsyncStreamWriter<TResponse>)this).WriteAsync(message, CancellationToken.None);
        }

        async Task IAsyncStreamWriter<TResponse>.WriteAsync(TResponse message, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(message);

            if (IsStreamingResponsesToClient)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                    _serverCallContext.CancellationToken,
                    cancellationToken);

                cts.Token.ThrowIfCancellationRequested();

                _incoming.LogTrace($"Sending response data to client in streaming mode.");
                using (await _serverCallContext.WriteMutex.WaitAsync(cts.Token).ConfigureAwait(false))
                {
                    await _incoming.Connection.WriteAsync(new TcpGrpcMessage
                    {
                        Type = TcpGrpcMessageType.ResponseData,
                    }, cts.Token).ConfigureAwait(false);
                    var serializationContext = new TcpGrpcSerializationContext();
                    _method.ResponseMarshaller.ContextualSerializer(message, serializationContext);
                    serializationContext.Complete();
                    await _incoming.Connection.WriteBlobAsync(serializationContext.Result, cts.Token).ConfigureAwait(false);
                }
                cts.Token.ThrowIfCancellationRequested();
            }
            else
            {
                throw new NotSupportedException("This call is not streaming requests to the server.");
            }
        }

        #endregion

        #region IAsyncStreamReader<TResponse> Implementation

        TRequest IAsyncStreamReader<TRequest>.Current
        {
            get
            {
                if (!IsStreamingRequestsFromClient)
                {
                    throw new NotSupportedException("This call is not streaming requests from the client.");
                }

                if (_requestData == null)
                {
                    throw new InvalidOperationException("Call MoveNext first before attempting to access the Current request.");
                }

                _serverCallContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();

                return _requestData;
            }
        }

        async Task<bool> IAsyncStreamReader<TRequest>.MoveNext(CancellationToken cancellationToken)
        {
            if (!IsStreamingRequestsFromClient)
            {
                throw new NotSupportedException("This call is not streaming requests from the client.");
            }

            _serverCallContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_serverCallContext.CancellationToken, cancellationToken);
            var (nextItem, terminated) = await _requestStream.TryDequeueAsync(cts.Token).ConfigureAwait(false);
            if (terminated)
            {
                return false;
            }
            _requestData = nextItem;
            return true;
        }

        #endregion
    }
}
