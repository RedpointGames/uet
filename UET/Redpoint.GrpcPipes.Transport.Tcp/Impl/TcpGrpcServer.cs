namespace Redpoint.GrpcPipes.Transport.Tcp.Impl
{
    using global::Grpc.Core;
    using Google.Protobuf.WellKnownTypes;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    internal sealed class TcpGrpcServer : ServiceBinderBase, IAsyncDisposable
    {
        private delegate Task CallHandlerAsync(
            TcpGrpcRequest request,
            TcpGrpcTransportConnection connection,
            string peer,
            Action<string> logTrace);

        private readonly TcpListener _listener;
        private readonly ILogger? _logger;
        private readonly CancellationTokenSource _cts;
        private readonly Task _loop;
        private readonly ConcurrentDictionary<TrackedCall, bool> _clients;
        private readonly Dictionary<string, CallHandlerAsync> _callHandlers;

        private class TrackedCall
        {
            public required TcpClient Client;
            public Task? Task;
        }

        public TcpGrpcServer(
            TcpListener listener,
            ILogger? logger = null)
        {
            _listener = listener;
            _listener.Start();
            _listener.Server.NoDelay = true;
            _logger = logger;
            _cts = new CancellationTokenSource();
            _clients = new ConcurrentDictionary<TrackedCall, bool>();
            _callHandlers = new Dictionary<string, CallHandlerAsync>();
            _loop = Task.Run(ProcessAsync);
        }

        private void LogTrace(string message)
        {
            if (_logger != null)
            {
                _logger.LogTrace($"TcpGrpcServer: {message}");
            }
        }

        private async Task ProcessAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                var nextCall = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                if (nextCall == null)
                {
                    continue;
                }

                LogTrace($"Accepted connection from '{nextCall.Client.RemoteEndPoint}'.");

                var tracked = new TrackedCall
                {
                    Client = nextCall
                };
                _clients.TryAdd(tracked, true);
                tracked.Task = Task.Run(async () => await ProcessCallAsync(tracked).ConfigureAwait(false));
            }
        }

        private async Task ProcessCallAsync(TrackedCall incomingCall)
        {
            try
            {
                await using (new TcpGrpcTransportConnection(incomingCall.Client, _logger, false).AsAsyncDisposable(out var connection))
                {
                    // Cached so we have it for logging even after the client is disposed.
                    var remoteEndpoint = incomingCall.Client.Client.RemoteEndPoint;

                    // Read the call initialization.
                    var request = await connection.ReadExpectedAsync<TcpGrpcRequest>(TcpGrpcRequest.Descriptor, _cts.Token).ConfigureAwait(false);
                    LogTrace($"'{remoteEndpoint}': Received request to call '{request.FullName}'.");

                    // Lookup the call handler.
                    if (!_callHandlers.TryGetValue(request.FullName, out var handler))
                    {
                        // This API method is not supported.
                        LogTrace($"'{remoteEndpoint}': Requested method is not implemented.");
                        await connection.WriteAsync(new TcpGrpcMessage
                        {
                            Type = TcpGrpcMessageType.ResponseComplete,
                        }, CancellationToken.None).ConfigureAwait(false);
                        await connection.WriteAsync(new TcpGrpcResponseComplete
                        {
                            StatusCode = (int)StatusCode.Unimplemented,
                            StatusDetails = "This method is not implemented.",
                            HasResponseTrailers = false,
                        }, CancellationToken.None).ConfigureAwait(false);
                        return;
                    }

                    // Invoke the call handler to handle the rest.
                    LogTrace($"'{remoteEndpoint}': Invoking call handler.");
                    await handler(
                        request,
                        connection,
                        remoteEndpoint!.AddressFamily switch
                        {
                            AddressFamily.InterNetwork => $"ipv4:{remoteEndpoint}",
                            AddressFamily.InterNetworkV6 => $"ipv6:{remoteEndpoint}",
                            _ => "unknown",
                        },
                        message =>
                        {
                            LogTrace($"'{remoteEndpoint}': {message}");
                        }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
            finally
            {
                _clients.TryRemove(incomingCall, out _);
            }
        }

        private async Task ProcessUnaryCallAsync<TRequest, TResponse>(
            TcpGrpcRequest request,
            TcpGrpcTransportConnection connection,
            string peer,
            Action<string> logTrace,
            Method<TRequest, TResponse> method,
            UnaryServerMethod<TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class
        {
            // Set up the call context.
            logTrace($"Creating server call context.");
            var writeMutex = new Mutex();
            using var serverCallContext = new TcpGrpcServerCallContext(
                method.Name,
                string.Empty,
                peer,
                request.DeadlineUnixTimeMilliseconds == 0
                    ? null
                    : DateTimeOffset.FromUnixTimeMilliseconds(request.DeadlineUnixTimeMilliseconds).UtcDateTime,
                request.HasRequestHeaders
                    ? TcpGrpcMetadataConverter.Convert(request.RequestHeaders)
                    : new Metadata(),
                connection,
                writeMutex,
                _cts.Token);
            async Task SendStatusAsync(StatusCode statusCode, string details)
            {
                try
                {
                    logTrace($"Waiting for write lock to send status ({statusCode}, '{details}') to client.");
                    using (await writeMutex.WaitAsync(serverCallContext.DeadlineCancellationToken).ConfigureAwait(false))
                    {
                        logTrace($"Writing of status ({statusCode}, '{details}') to client.");
                        await connection.WriteAsync(new TcpGrpcMessage
                        {
                            Type = TcpGrpcMessageType.ResponseComplete,
                        }, serverCallContext.DeadlineCancellationToken).ConfigureAwait(false);
                        await connection.WriteAsync(new TcpGrpcResponseComplete
                        {
                            StatusCode = (int)statusCode,
                            StatusDetails = details,
                            HasResponseTrailers = serverCallContext.ResponseTrailers.Count > 0,
                            ResponseTrailers = TcpGrpcMetadataConverter.Convert(serverCallContext.ResponseTrailers),
                        }, serverCallContext.DeadlineCancellationToken).ConfigureAwait(false);
                        logTrace($"Wrote status ({statusCode}, '{details}') to client.");
                    }
                }
                catch (OperationCanceledException) when (serverCallContext.DeadlineCancellationToken.IsCancellationRequested)
                {
                    // We can't send any content to the client, because we have exceeded our extended deadline cancellation.
                    logTrace($"Unable to send ({statusCode}, '{details}') to client because the call has already exceeded the extended deadline cancellation.");
                    return;
                }
            }

            // Read the message type (it must be RequestData).
            logTrace($"Reading next message type from client.");
            var message = await connection.ReadExpectedAsync<TcpGrpcMessage>(
                TcpGrpcMessage.Descriptor,
                serverCallContext.CancellationToken).ConfigureAwait(false);
            if (message.Type == TcpGrpcMessageType.RequestCancel)
            {
                // The client cancelled the request, so we don't need to send
                // a status to them.
                serverCallContext.CancellationTokenSource.Cancel();
                return;
            }
            if (message.Type != TcpGrpcMessageType.RequestData)
            {
                await SendStatusAsync(
                    StatusCode.Internal,
                    "Client did not send request data.").ConfigureAwait(false);
                return;
            }

            // Read the request data.
            TRequest requestData;
            {
                logTrace($"Reading request data from client.");
                using var memory = await connection.ReadBlobAsync(serverCallContext.CancellationToken).ConfigureAwait(false);
                requestData = method.RequestMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
            }

            // Invoke the handler, and monitor for client side cancellation.
            using var stopCalls = CancellationTokenSource.CreateLinkedTokenSource(serverCallContext.CancellationToken);
            var receiveCancelTask = Task.Run(async () =>
            {
                // Read the next message to see if we get cancelled.
                while (!stopCalls.IsCancellationRequested && !connection.HasReadBeenInterrupted)
                {
                    TcpGrpcMessage message;
                    try
                    {
                        message = await connection.ReadExpectedAsync<TcpGrpcMessage>(
                            TcpGrpcMessage.Descriptor,
                            stopCalls.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        // The invocation completed and calls were stopped.
                        return;
                    }
                    catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.Unavailable)
                    {
                        // The client closed the connection. 
                        logTrace($"Server is cancelling the operation as the client closed the connection.");
                        serverCallContext.CancellationTokenSource.Cancel();
                        return;
                    }
                    if (message.Type == TcpGrpcMessageType.RequestCancel)
                    {
                        // We don't need to send cancellation here; if it's relevant it'll
                        // be handled in invokeTask.
                        logTrace($"Server received cancellation from client.");
                        serverCallContext.CancellationTokenSource.Cancel();
                        return;
                    }
                }
            });
            var invokeTask = Task.Run(async () =>
            {
                // Invoke the handler.
                TResponse response;
                try
                {
                    logTrace($"Invoking server-side implementation of method.");
                    response = await handler(requestData, serverCallContext).ConfigureAwait(false);
                }
                catch (RpcException ex)
                {
                    logTrace($"Server-side method implementation threw RpcException.");
                    await SendStatusAsync(
                        ex.Status.StatusCode,
                        ex.Status.Detail).ConfigureAwait(false);
                    return;
                }
                catch (OperationCanceledException)
                {
                    logTrace($"Server-side method implementation threw OperationCanceledException.");
                    await SendStatusAsync(
                        Status.DefaultCancelled.StatusCode,
                        Status.DefaultCancelled.Detail).ConfigureAwait(false);
                    return;
                }
                catch (Exception ex)
                {
                    logTrace($"Server-side method implementation threw an unknown exception: {ex}");
                    await SendStatusAsync(
                        StatusCode.Unknown,
                        ex.ToString()).ConfigureAwait(false);
                    return;
                }

                // The call is complete.
                try
                {
                    logTrace($"Waiting for write lock to write response to client.");
                    using (await writeMutex.WaitAsync(serverCallContext.CancellationToken).ConfigureAwait(false))
                    {
                        logTrace($"Writing response to client.");
                        await connection.WriteAsync(new TcpGrpcMessage
                        {
                            Type = TcpGrpcMessageType.ResponseData,
                        }, serverCallContext.CancellationToken).ConfigureAwait(false);
                        var serializationContext = new TcpGrpcSerializationContext();
                        method.ResponseMarshaller.ContextualSerializer(response, serializationContext);
                        serializationContext.Complete();
                        await connection.WriteBlobAsync(
                            serializationContext.Result,
                            serverCallContext.CancellationToken)
                            .ConfigureAwait(false);
                        logTrace($"Wrote response to client.");
                    }
                    await SendStatusAsync(
                        Status.DefaultSuccess.StatusCode,
                        Status.DefaultSuccess.Detail).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    logTrace($"Server handling was cancelled before response could be entirely written.");
                }
                return;
            });
            await Task.WhenAny(receiveCancelTask, invokeTask).ConfigureAwait(false);
            stopCalls.Cancel();
            await Task.WhenAll(receiveCancelTask, invokeTask).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
                LogTrace("Waiting for accept loop to stop.");
                await _loop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            try
            {
                foreach (var client in _clients.Keys.ToArray())
                {
                    try
                    {
                        LogTrace("Waiting for client handler stop.");
                        await client.Task!.ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
            _listener.Stop();
            _listener.Dispose();
            _cts.Dispose();
            LogTrace("TCP gRPC server has been shutdown.");
        }

        #region ServiceBinderBase

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            UnaryServerMethod<TRequest, TResponse> handler)
        {
            ArgumentNullException.ThrowIfNull(method);
            _callHandlers[method.FullName] = async (request, connection, peer, logTrace) =>
            {
                await ProcessUnaryCallAsync(
                    request,
                    connection,
                    peer,
                    logTrace,
                    method,
                    handler).ConfigureAwait(false);
            };
        }

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            ClientStreamingServerMethod<TRequest, TResponse> handler)
        {
            // base.AddMethod(method, handler);
        }

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            ServerStreamingServerMethod<TRequest, TResponse> handler)
        {
            //base.AddMethod(method, handler);
        }

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            DuplexStreamingServerMethod<TRequest, TResponse> handler)
        {
            //base.AddMethod(method, handler);
        }

        #endregion
    }
}
