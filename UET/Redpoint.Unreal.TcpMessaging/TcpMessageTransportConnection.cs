namespace Redpoint.Unreal.TcpMessaging
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Unreal.Serialization;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public class TcpMessageTransportConnection : IDisposable
    {
        private readonly Guid _guid;
        private readonly TcpClient _client;
        private Guid? _remoteNodeId;
        private bool _receivedHeader;
        private bool _disposed;
        private ConcurrentQueue<TcpDeserializedMessage> _queuedToSend;
        private Task _backgroundSender;
        private readonly ILogger? _logger;
        private SemaphoreSlim _readyToSend;

        public static async Task<TcpMessageTransportConnection> CreateAsync(TcpClient client, ILogger? logger = null)
        {
            var instance = new TcpMessageTransportConnection(client, logger);
            await instance.SendHeader();
            instance._disposed = false;
            instance._queuedToSend = new ConcurrentQueue<TcpDeserializedMessage>();
            instance._readyToSend = new SemaphoreSlim(0);
            instance._backgroundSender = Task.Run(instance.SendInBackground);
            return instance;
        }

#pragma warning disable CS8618
        private TcpMessageTransportConnection(TcpClient client, ILogger? logger)
        {
            _logger = logger;
            _guid = Guid.NewGuid();
            _client = client;
            if (!_client.Connected)
            {
                throw new InvalidOperationException();
            }
            _remoteNodeId = null;
            _receivedHeader = false;
        }
#pragma warning restore CS8618

        private async Task SendInBackground()
        {
            while (!_disposed)
            {
                try
                {
                    await _readyToSend.WaitAsync();

                    TcpDeserializedMessage nextMessageRaw;
                    if (!_queuedToSend.TryDequeue(out nextMessageRaw!))
                    {
                        throw new InvalidOperationException();
                    }
                    Store<TcpDeserializedMessage> nextMessage = new(nextMessageRaw);

                    using (var memory = new MemoryStream())
                    {
                        var memoryArchive = new Archive(memory, false);

                        _logger?.LogTrace($" {{{(nextMessage.V.RecipientAddresses.V.Data.Length > 0 ? nextMessage.V.RecipientAddresses.V.Data[0].UniqueId : "*")}}} <- {{{nextMessage.V.SenderAddress.V.UniqueId}}} [{nextMessage.V.AssetPath.V.PackageName + "." + nextMessage.V.AssetPath.V.AssetName}]\n{nextMessage.V.GetMessageData()}");

                        await memoryArchive.Serialize(nextMessage);

                        var length = new Store<uint>((uint)memory.Position);
                        if (length.V == 0)
                        {
                            throw new InvalidOperationException();
                        }

                        var sendArchive = new Archive(_client.GetStream(), false);

                        await sendArchive.Serialize(length);

                        memory.Seek(0, SeekOrigin.Begin);
                        await memory.CopyToAsync(_client.GetStream());

                        _logger?.LogTrace("Flushed message to remote TCP stream!");
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogCritical(ex, $"Failed to serialize message: {ex.Message}");
                }
            }
        }

        private async Task SendHeader()
        {
            using (var memoryStream = new MemoryStream())
            {
                Store<TcpMessageHeader> header = new(new(_guid));

                var headerArchive = new Archive(memoryStream, false);
                await headerArchive.Serialize(header);

                var position = memoryStream.Position;
                memoryStream.Seek(0, SeekOrigin.Begin);

                await _client.GetStream().WriteAsync(memoryStream.GetBuffer(), 0, (int)position);
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
            try
            {
                var receiveArchive = new Archive(_client.GetStream(), true);

                if (!_receivedHeader)
                {
                    var header = new Store<TcpMessageHeader>(new());
                    await receiveArchive.Serialize(header);
                    _remoteNodeId = header.V.NodeId;
                    _receivedHeader = true;
                    _logger?.LogTrace($"Received header from remote node ID {_remoteNodeId}");
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    var nextBytes = new Store<uint>(0);
                    await receiveArchive.Serialize(nextBytes);

                    var buffer = new Store<byte[]>(new byte[0]);
                    await receiveArchive.Serialize(buffer, nextBytes.V);

                    var nextMessage = new Store<TcpDeserializedMessage>(new());
                    try
                    {
                        using (var memory = new MemoryStream(buffer.V))
                        {
                            var memoryArchive = new Archive(memory, true);
                            await memoryArchive.Serialize(nextMessage);
                        }
                    }
                    catch (TopLevelAssetPathNotFoundException)
                    {
                        _logger?.LogTrace($" {{{nextMessage.V.SenderAddress.V.UniqueId.V}}} -> {{{(nextMessage.V.RecipientAddresses.V.Data.Length > 0 ? nextMessage.V.RecipientAddresses.V.Data[0].UniqueId.V : "*")}}} [{nextMessage.V.AssetPath.V.PackageName.V + "." + nextMessage.V.AssetPath.V.AssetName.V}]\n(no C# class registered for this message type)");
                        continue;
                    }

                    {
                        _logger?.LogTrace($" {{{nextMessage.V.SenderAddress.V.UniqueId.V}}} -> {{{(nextMessage.V.RecipientAddresses.V.Data.Length > 0 ? nextMessage.V.RecipientAddresses.V.Data[0].UniqueId.V : "*")}}} [{nextMessage.V.AssetPath.V.PackageName.V + "." + nextMessage.V.AssetPath.V.AssetName.V}]\n{nextMessage.V.GetMessageData()}");

                        if (await onMessageReceived(nextMessage.V))
                        {
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogCritical(ex, $"Exception during message receive: {ex.Message}");
                throw;
            }
        }

        public void Dispose()
        {
            _disposed = true;
            ((IDisposable)_client).Dispose();
        }
    }
}
