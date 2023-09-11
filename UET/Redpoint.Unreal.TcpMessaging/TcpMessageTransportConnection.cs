using System;

namespace Redpoint.Unreal.TcpMessaging
{
    using Redpoint.Concurrency;
    using Microsoft.Extensions.Logging;
    using Redpoint.Unreal.Serialization;
    using Redpoint.Unreal.TcpMessaging.MessageTypes;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    public class TcpMessageTransportConnection : IAsyncDisposable
    {
        private readonly ILogger? _logger;
        private readonly Guid _guid;
        private readonly TcpReconnectableStream _stream;

        private class AttachedListener
        {
            public Concurrency.Semaphore Ready = new Concurrency.Semaphore(0);
            public ConcurrentQueue<TcpDeserializedMessage> Queue = new ConcurrentQueue<TcpDeserializedMessage>();
        }

        private Guid? _remoteNodeId;
        private bool _disposed;
        private readonly CancellationTokenSource _disposeCancellationTokenSource;
        private ConcurrentQueue<TcpDeserializedMessage> _queuedToSend;
        private Task _backgroundSender;
        private readonly Task _backgroundReceiver;
        private readonly Task _backgroundPinger;
        private readonly List<AttachedListener> _listeningQueues;
        private bool _isLegacyTcpSerialization;
        private Concurrency.Semaphore _readyToSend;
        private string _remoteOwner;

        private static ISerializerRegistry[] _serializerRegistries = new[]
        {
            new TcpMessagingUnrealSerializerRegistry()
        };

        public static async Task<TcpMessageTransportConnection> CreateAsync(Func<Task<TcpClient>> connectionFactory, ILogger? logger = null)
        {
            if (connectionFactory == null) throw new ArgumentNullException(nameof(connectionFactory));

            Guid initialRemoteGuidId = Guid.Empty;
            TcpMessageTransportConnection? instance = null;
            var guid = Guid.NewGuid();
            var reconnectableStream = await TcpReconnectableStream.CreateAsync(
                logger,
                connectionFactory,
                async client =>
                {
                    // Write our header to the stream.
                    using (var memoryStream = new MemoryStream())
                    {
                        Store<TcpMessageHeader> header = new(new(guid));

                        var headerArchive = new Archive(memoryStream, false, _serializerRegistries);
                        await headerArchive.Serialize(header).ConfigureAwait(false);

                        var position = memoryStream.Position;
                        memoryStream.Seek(0, SeekOrigin.Begin);

                        await client.GetStream().WriteAsync(memoryStream.GetBuffer().AsMemory(0, (int)position)).ConfigureAwait(false);
                    }

                    // Receive the remote's header from the stream.
                    {
                        var receiveArchive = new Archive(client.GetStream(), true, _serializerRegistries);
                        var header = new Store<TcpMessageHeader>(new());
                        await receiveArchive.Serialize(header).ConfigureAwait(false);
                        if (instance == null)
                        {
                            initialRemoteGuidId = header.V.NodeId;
                        }
                        else
                        {
                            instance._remoteNodeId = header.V.NodeId;
                        }
                        logger?.LogTrace($"Received header from remote node ID {header.V.NodeId}");
                    }
                }).ConfigureAwait(false);
            instance = new TcpMessageTransportConnection(reconnectableStream, guid, initialRemoteGuidId, logger);
            return instance;
        }

        public event EventHandler? OnUnrecoverablyBroken;

        private TcpMessageTransportConnection(TcpReconnectableStream stream, Guid guid, Guid initialRemoteGuidId, ILogger? logger)
        {
            _logger = logger;
            _guid = guid;
            _stream = stream;

            _disposed = false;
            _disposeCancellationTokenSource = new CancellationTokenSource();
            _remoteNodeId = initialRemoteGuidId;
            _queuedToSend = new ConcurrentQueue<TcpDeserializedMessage>();
            _readyToSend = new Concurrency.Semaphore(0);
            _backgroundSender = Task.Run(SendInBackground);
            _backgroundReceiver = Task.Run(ReceiveInBackground);
            _backgroundPinger = Task.Run(PingInBackground);
            _listeningQueues = new List<AttachedListener>();
            _isLegacyTcpSerialization = false;

            _remoteOwner = string.Empty;
        }

        private async Task PingInBackground()
        {
            while (!_disposed)
            {
                Send(new EngineServicePing());
                Send(new SessionServicePing { UserName = _remoteOwner ?? Environment.UserName ?? "user" });
                await Task.Delay(2000).ConfigureAwait(false);
            }
        }

        public Guid? RemoteSessionId { get; private set; }

        private async Task ReceiveInBackground()
        {
            bool gotEnginePong = false;
            int engineVersion = 0;
            Guid engineSessionId = Guid.Empty;
            bool gotSessionPong = false;
            string buildDate = string.Empty;

            while (!_disposed)
            {
                try
                {
                    var receiveArchive = new Archive(_stream, true, _serializerRegistries);

                    var nextBytes = new Store<uint>(0);
                    await receiveArchive.Serialize(nextBytes).ConfigureAwait(false);

                    var buffer = new Store<byte[]>(Array.Empty<byte>());
                    await receiveArchive.Serialize(buffer, nextBytes.V).ConfigureAwait(false);

                    var nextMessage = new Store<TcpDeserializedMessage>(new());
                    try
                    {
                        if (_isLegacyTcpSerialization)
                        {
                            var legacyNextMessage = new Store<LegacyTcpDeserializedMessage>(new());
                            using (var memory = new MemoryStream(buffer.V))
                            {
                                var memoryArchive = new Archive(memory, true, _serializerRegistries);
                                await memoryArchive.Serialize(legacyNextMessage).ConfigureAwait(false);
                            }
                            nextMessage.V = legacyNextMessage.V.ToModernMessage();
                        }
                        else
                        {
                            using (var memory = new MemoryStream(buffer.V))
                            {
                                var memoryArchive = new Archive(memory, true, _serializerRegistries);
                                await memoryArchive.Serialize(nextMessage).ConfigureAwait(false);
                            }
                        }
                    }
                    catch (TopLevelAssetPathNotFoundException)
                    {
                        _logger?.LogTrace($" {{{nextMessage.V.SenderAddress.V.UniqueId.V}}} -> {{{(nextMessage.V.RecipientAddresses.V.Data.Count > 0 ? nextMessage.V.RecipientAddresses.V.Data[0].UniqueId.V : "*")}}} [{nextMessage.V.AssetPath.V.PackageName.V + "." + nextMessage.V.AssetPath.V.AssetName.V}]\n(no C# class registered for this message type)");
                        continue;
                    }
                    catch (Exception ex) when ((!_isLegacyTcpSerialization) && (ex is EndOfStreamException || ex is OverflowException))
                    {
                        var legacyNextMessage = new Store<LegacyTcpDeserializedMessage>(new());
                        var shouldContinue = false;
                        try
                        {
                            // Try legacy deserialization.
                            using (var memory = new MemoryStream(buffer.V))
                            {
                                var memoryArchive = new Archive(memory, true, _serializerRegistries);
                                await memoryArchive.Serialize(legacyNextMessage).ConfigureAwait(false);
                            }
                            nextMessage.V = legacyNextMessage.V.ToModernMessage();
                        }
                        catch (TopLevelAssetPathNotFoundException)
                        {
                            // We still decoded the header properly, so we still turn on legacy serialization in this case.
                            _logger?.LogTrace($" {{{legacyNextMessage.V.SenderAddress.V.UniqueId.V}}} -> {{{(legacyNextMessage.V.RecipientAddresses.V.Data.Count > 0 ? legacyNextMessage.V.RecipientAddresses.V.Data[0].UniqueId.V : "*")}}} [{legacyNextMessage.V.AssetPath.V}]\n(no C# class registered for this message type)");
                            shouldContinue = true;
                        }

                        // We successfully decoded via legacy serialization.
                        _logger?.LogTrace("Detected serialization mode used by Unreal Engine 5.0, switching to legacy serialization.");
                        _isLegacyTcpSerialization = true;
                        if (shouldContinue)
                        {
                            continue;
                        }
                    }

                    {
                        _logger?.LogTrace($" {{{nextMessage.V.SenderAddress.V.UniqueId.V}}} -> {{{(nextMessage.V.RecipientAddresses.V.Data.Count > 0 ? nextMessage.V.RecipientAddresses.V.Data[0].UniqueId.V : "*")}}} [{nextMessage.V.AssetPath.V.PackageName.V + "." + nextMessage.V.AssetPath.V.AssetName.V}]\n{nextMessage.V.GetMessageData()}");

                        switch (nextMessage.V.GetMessageData())
                        {
                            case EngineServicePong pong:
                                gotEnginePong = true;
                                engineVersion = pong.EngineVersion;
                                engineSessionId = pong.SessionId;
                                _logger?.LogTrace($"Got EngineServicePong EngineVersion={engineVersion} EngineSessionId={engineSessionId}");
                                break;
                            case EngineServicePing ping:
                                if (gotEnginePong)
                                {
                                    Respond(nextMessage.V, new EngineServicePong
                                    {
                                        EngineVersion = engineVersion,
                                        InstanceId = _guid,
                                        SessionId = engineSessionId,
                                        InstanceType = "Editor",
                                        CurrentLevel = string.Empty,
                                        HasBegunPlay = false,
                                        WorldTimeSeconds = 0.0f,
                                    });
                                }
                                break;
                            case SessionServicePong pong:
                                gotSessionPong = true;
                                RemoteSessionId = pong.SessionId;
                                buildDate = pong.BuildDate;
                                _remoteOwner = pong.SessionOwner;
                                _logger?.LogTrace($"Got SessionServicePong RemoteSessionId={RemoteSessionId} BuildDate={pong.BuildDate} SessionOwner={pong.SessionOwner}");
                                break;
                            case SessionServicePing ping:
                                if (gotSessionPong)
                                {
                                    Respond(nextMessage.V, new SessionServicePong
                                    {
                                        Authorized = true,
                                        BuildDate = buildDate,
                                        DeviceName = "UET",
                                        InstanceId = _guid,
                                        InstanceName = $"UET-{Environment.ProcessId}",
                                        PlatformName = "WindowsEditor",
                                        SessionId = RemoteSessionId!.Value,
                                        SessionName = string.Empty,
                                        SessionOwner = _remoteOwner,
                                        Standalone = false,
                                    });
                                }
                                break;
                        }

                        foreach (var queue in _listeningQueues.ToArray())
                        {
                            queue.Queue.Enqueue(nextMessage.V);
                            queue.Ready.Release();
                        }
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException && ((SocketException)ex.InnerException).SocketErrorCode == SocketError.OperationAborted)
                {
                    // Transport connection is shutting down.
                    return;
                }
                catch (OperationCanceledException)
                {
                    // Transport connection is shutting down.
                    return;
                }
                catch (TcpReconnectionRequiredException)
                {
                    if (_disposed)
                    {
                        // Do not reconnect because we're already disposed.
                        _logger?.LogTrace("Ignoring TcpReconnectionRequiredException during receive because this TcpMessageTransportConnection is disposed.");
                    }
                    else
                    {
                        // The connection must be reconnected.
                        _logger?.LogTrace("TCP stream disconnected during received, reconnecting...");
                        await _stream.ReconnectAsync().ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Exception during message receive: {ex.Message}");
                    if (OnUnrecoverablyBroken != null)
                    {
                        OnUnrecoverablyBroken(this, new EventArgs());
                    }
                    await DisposeAsync().ConfigureAwait(false);
                    throw;
                }
            }
        }

        private async Task SendInBackground()
        {
            while (!_disposed)
            {
                try
                {
                    await _readyToSend.WaitAsync(_disposeCancellationTokenSource.Token).ConfigureAwait(false);

                    TcpDeserializedMessage nextMessageRaw;
                    if (!_queuedToSend.TryDequeue(out nextMessageRaw!))
                    {
                        throw new InvalidOperationException();
                    }

                    try
                    {
                        Store<TcpDeserializedMessage> nextMessage = new(nextMessageRaw);

                        using (var memory = new MemoryStream())
                        {
                            var memoryArchive = new Archive(memory, false, _serializerRegistries);

                            _logger?.LogTrace($" {{{(nextMessage.V.RecipientAddresses.V.Data.Count > 0 ? nextMessage.V.RecipientAddresses.V.Data[0].UniqueId : "*")}}} <- {{{nextMessage.V.SenderAddress.V.UniqueId}}} [{nextMessage.V.AssetPath.V.PackageName + "." + nextMessage.V.AssetPath.V.AssetName}]\n{nextMessage.V.GetMessageData()}");

                            if (_isLegacyTcpSerialization)
                            {
                                var legacyNextMessage = new Store<LegacyTcpDeserializedMessage>(nextMessage.V.ToLegacyMessage());
                                await memoryArchive.Serialize(legacyNextMessage).ConfigureAwait(false);
                            }
                            else
                            {
                                await memoryArchive.Serialize(nextMessage).ConfigureAwait(false);
                            }

                            var length = new Store<uint>((uint)memory.Position);
                            if (length.V == 0)
                            {
                                throw new InvalidOperationException();
                            }

                            var sendArchive = new Archive(_stream, false, _serializerRegistries);

                            await sendArchive.Serialize(length).ConfigureAwait(false);

                            memory.Seek(0, SeekOrigin.Begin);
                            await memory.CopyToAsync(_stream).ConfigureAwait(false);

                            _logger?.LogTrace("Flushed message to remote TCP stream!");
                        }
                    }
                    catch (TcpReconnectionRequiredException)
                    {
                        if (_disposed)
                        {
                            // Do not reconnect because we're already disposed.
                            _logger?.LogTrace("Ignoring TcpReconnectionRequiredException during send because this TcpMessageTransportConnection is disposed.");
                            return;
                        }
                        else
                        {
                            // The connection must be reconnected and the message resent.
                            _logger?.LogTrace("TCP stream disconnected during send, reconnecting...");
                            await _stream.ReconnectAsync().ConfigureAwait(false);
                            _queuedToSend.Enqueue(nextMessageRaw);
                            _readyToSend.Release();
                            continue;
                        }
                    }
                }
                catch (InvalidOperationException ex) when (ex.Message.Contains("The operation is not allowed on non-connected sockets", StringComparison.Ordinal))
                {
                    // This is expected when the connection is being intentionally closed.
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, $"Failed to serialize message: {ex.Message}");
                }
            }
        }

        public void Send<T>(MessageAddress targetAddress, T value) where T : notnull, new()
        {
            var nextMessage = new TcpDeserializedMessage
            {
                SenderAddress = new(new MessageAddress(_guid)),
                RecipientAddresses = new(new ArchiveArray<int, MessageAddress>(new[] { targetAddress })),
                MessageScope = new(MessageScope.All),
                TimeSent = new(DateTimeOffset.UtcNow),
                ExpirationTime = new(DateTimeOffset.MaxValue),
            };
            nextMessage.SetMessageData(value);

            _queuedToSend.Enqueue(nextMessage);
            _readyToSend.Release();
        }

        public void Send<T>(T value) where T : notnull, new()
        {
            var nextMessage = new TcpDeserializedMessage
            {
                SenderAddress = new(new MessageAddress(_guid)),
                MessageScope = new(MessageScope.All),
                TimeSent = new(DateTimeOffset.UtcNow),
                ExpirationTime = new(DateTimeOffset.MaxValue),
            };
            nextMessage.SetMessageData(value);

            _queuedToSend.Enqueue(nextMessage);
            _readyToSend.Release();
        }

        public void Respond<T>(TcpDeserializedMessage sourceMessage, T value) where T : notnull, new()
        {
            if (sourceMessage == null) throw new ArgumentNullException(nameof(sourceMessage));

            var nextMessage = new TcpDeserializedMessage
            {
                SenderAddress = new(new MessageAddress(_guid)),
                RecipientAddresses = new(new ArchiveArray<int, MessageAddress>(new MessageAddress[] { sourceMessage.SenderAddress.V })),
                MessageScope = new(MessageScope.All),
                TimeSent = new(DateTimeOffset.UtcNow),
                ExpirationTime = new(DateTimeOffset.MaxValue),
            };
            nextMessage.SetMessageData(value);

            _queuedToSend.Enqueue(nextMessage);
            _readyToSend.Release();
        }

        public async Task ReceiveUntilAsync(Func<TcpDeserializedMessage, Task<bool>> onMessageReceived, CancellationToken cancellationToken)
        {
            if (onMessageReceived == null) throw new ArgumentNullException(nameof(onMessageReceived));

            try
            {
                var listener = new AttachedListener();
                _listeningQueues.Add(listener);
                try
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await listener.Ready.WaitAsync(cancellationToken).ConfigureAwait(false);

                        TcpDeserializedMessage nextMessage;
                        var didPull = false;
                        do
                        {
                            didPull = listener.Queue.TryDequeue(out nextMessage!);
                        }
                        while (!didPull);

                        if (await onMessageReceived(nextMessage).ConfigureAwait(false))
                        {
                            return;
                        }
                    }
                }
                finally
                {
                    _listeningQueues.Remove(listener);
                }
            }
            catch (OperationCanceledException)
            {
                // Operation has been cancelled.
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, $"Exception during message receive: {ex.Message}");
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);
            _disposed = true;
            _disposeCancellationTokenSource.Cancel();
            _disposeCancellationTokenSource.Dispose();
            await _stream.DisposeAsync().ConfigureAwait(false);
        }
    }
}
