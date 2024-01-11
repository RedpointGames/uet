namespace Redpoint.Grpc.Transport.Tcp
{
    using global::Grpc.Core;
    using Google.Protobuf.WellKnownTypes;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public sealed class TcpGrpcServer : ServiceBinderBase, IAsyncDisposable
    {
        private delegate Task CallHandlerAsync(
            TcpGrpcRequest request,
            TcpGrpcTransportConnection connection);

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts;
        private readonly Task _loop;
        private readonly ConcurrentDictionary<TrackedCall, bool> _clients;
        private readonly Dictionary<string, CallHandlerAsync> _callHandlers;

        private class TrackedCall
        {
            public required TcpClient Client;
            public Task? Task;
        }

        public TcpGrpcServer(TcpListener listener)
        {
            _listener = listener;
            _cts = new CancellationTokenSource();
            _clients = new ConcurrentDictionary<TrackedCall, bool>();
            _callHandlers = new Dictionary<string, CallHandlerAsync>();
            _loop = Task.Run(ProcessAsync);
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
                await using (new TcpGrpcTransportConnection(incomingCall.Client).AsAsyncDisposable(out var connection))
                {
                    // Read the call initialization.
                    var request = await connection.ReadExpectedAsync<TcpGrpcRequest>(TcpGrpcRequest.Descriptor, _cts.Token).ConfigureAwait(false);

                    // Lookup the call handler.
                    if (!_callHandlers.TryGetValue(request.FullName, out var handler))
                    {
                        // This API method is not supported.
                        await connection.WriteAsync(new TcpGrpcMessage
                        {
                            Type = TcpGrpcMessageType.ResponseComplete,
                        }).ConfigureAwait(false);
                        await connection.WriteAsync(new TcpGrpcResponseComplete
                        {
                            StatusCode = (int)StatusCode.Unimplemented,
                            StatusDetails = "This method is not implemented.",
                            HasResponseTrailers = false,
                        }).ConfigureAwait(false);
                        return;
                    }

                    // Invoke the call handler to handle the rest.
                    await handler(request, connection).ConfigureAwait(false);
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
            Method<TRequest, TResponse> method,
            UnaryServerMethod<TRequest, TResponse> handler)
            where TRequest : class
            where TResponse : class
        {
            // Set up the call context.
            var writeMutex = new Mutex();
            using var serverCallContext = new TcpGrpcServerCallContext(
                method.Name,
                string.Empty,
                // @todo: We must propagate this from the TcpClient.
                string.Empty,
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
                using (await writeMutex.WaitAsync(serverCallContext.CancellationToken).ConfigureAwait(false))
                {
                    await connection.WriteAsync(new TcpGrpcMessage
                    {
                        Type = TcpGrpcMessageType.ResponseComplete,
                    }).ConfigureAwait(false);
                    await connection.WriteAsync(new TcpGrpcResponseComplete
                    {
                        StatusCode = (int)statusCode,
                        StatusDetails = details,
                        HasResponseTrailers = serverCallContext.ResponseTrailers.Count > 0,
                        ResponseTrailers = TcpGrpcMetadataConverter.Convert(serverCallContext.ResponseTrailers),
                    }).ConfigureAwait(false);
                }
            }

            // Read the message type (it must be RequestData).
            var message = await connection.ReadExpectedAsync<TcpGrpcMessage>(
                TcpGrpcMessage.Descriptor,
                serverCallContext.CancellationToken).ConfigureAwait(false);
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
                using var memory = await connection.ReadBlobAsync(serverCallContext.CancellationToken).ConfigureAwait(false);
                requestData = method.RequestMarshaller.ContextualDeserializer(new TcpGrpcDeserializationContext(memory.Memory));
            }

            // Invoke the handler.
            TResponse response;
            try
            {
                response = await handler(requestData, serverCallContext).ConfigureAwait(false);
            }
            catch (RpcException ex)
            {
                await SendStatusAsync(
                    ex.Status.StatusCode,
                    ex.Status.Detail).ConfigureAwait(false);
                return;
            }
            catch (OperationCanceledException)
            {
                await SendStatusAsync(
                    Status.DefaultCancelled.StatusCode,
                    Status.DefaultCancelled.Detail).ConfigureAwait(false);
                return;
            }
            catch (Exception ex)
            {
                await SendStatusAsync(
                    StatusCode.Unknown,
                    ex.ToString()).ConfigureAwait(false);
                return;
            }

            // The call is complete.
            using (await writeMutex.WaitAsync(serverCallContext.CancellationToken).ConfigureAwait(false))
            {
                await connection.WriteAsync(new TcpGrpcMessage
                {
                    Type = TcpGrpcMessageType.ResponseData,
                }).ConfigureAwait(false);
                var serializationContext = new TcpGrpcSerializationContext();
                method.ResponseMarshaller.ContextualSerializer(response, serializationContext);
                serializationContext.Complete();
                await connection.WriteBlobAsync(serializationContext.Result)
                    .ConfigureAwait(false);
            }
            await SendStatusAsync(
                Status.DefaultSuccess.StatusCode,
                Status.DefaultSuccess.Detail).ConfigureAwait(false);
            return;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
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
            _listener.Dispose();
            _cts.Dispose();
        }

        #region ServiceBinderBase

        public override void AddMethod<TRequest, TResponse>(
            Method<TRequest, TResponse> method,
            UnaryServerMethod<TRequest, TResponse> handler)
        {
            ArgumentNullException.ThrowIfNull(method);
            _callHandlers[method.FullName] = async (request, connection) =>
            {
                await ProcessUnaryCallAsync(
                    request,
                    connection,
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
