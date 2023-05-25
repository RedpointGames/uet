namespace Redpoint.Unreal.TcpMessaging
{
    using Redpoint.Unreal.Serialization;
    using System.Collections.Concurrent;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    public class TcpMessageTransportConnection : IDisposable
    {
        private object _consoleLock;
        private readonly Guid _guid;
        private readonly TcpClient _client;
        private Guid? _remoteNodeId;
        private bool _receivedHeader;
        private bool _disposed;
        private ConcurrentQueue<TcpDeserializedMessage> _queuedToSend;
        private Task _backgroundSender;
        private readonly bool _silent;
        private SemaphoreSlim _readyToSend;

        public TcpMessageTransportConnection(IPEndPoint targetEndpoint, bool silent = true)
        {
            _consoleLock = new object();
            _guid = Guid.NewGuid();
            _client = new TcpClient();
            _client.Connect(targetEndpoint);
            _remoteNodeId = null;
            _receivedHeader = false;
            SendHeader();
            _disposed = false;
            _queuedToSend = new ConcurrentQueue<TcpDeserializedMessage>();
            _readyToSend = new SemaphoreSlim(0);
            _backgroundSender = Task.Run(SendInBackground);
            _silent = silent;
        }

        private async Task SendInBackground()
        {
            while (!_disposed)
            {
                await _readyToSend.WaitAsync();

                TcpDeserializedMessage nextMessage;
                if (!_queuedToSend.TryDequeue(out nextMessage!))
                {
                    throw new InvalidOperationException();
                }

                using (var memory = new MemoryStream())
                {
                    var memoryArchive = new Archive(memory, false);

                    if (!_silent)
                    {
                        lock (_consoleLock)
                        {
                            var oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write($"{nextMessage.TimeSent}");
                            Console.ForegroundColor = oldColor;
                            Console.WriteLine($" {{{(nextMessage.RecipientAddresses.Data.Length > 0 ? nextMessage.RecipientAddresses.Data[0].UniqueId : "*")}}} <- {{{nextMessage.SenderAddress.UniqueId}}} [{nextMessage.AssetPath.PackageName + "." + nextMessage.AssetPath.AssetName}]\n{nextMessage.GetMessageData()}");
                        }
                    }

                    memoryArchive.Serialize(ref nextMessage);

                    uint length = (uint)memory.Position;
                    if (length == 0)
                    {
                        throw new InvalidOperationException();
                    }

                    var sendArchive = new Archive(_client.GetStream(), false);

                    sendArchive.Serialize(ref length);

                    memory.Seek(0, SeekOrigin.Begin);
                    memory.CopyTo(_client.GetStream());
                }
            }
        }

        private void SendHeader()
        {
            using (var memoryStream = new MemoryStream())
            {
                var header = new TcpMessageHeader(_guid);

                var headerArchive = new Archive(memoryStream, false);
                headerArchive.Serialize(ref header);

                var position = memoryStream.Position;
                memoryStream.Seek(0, SeekOrigin.Begin);

                _client.GetStream().Write(memoryStream.GetBuffer(), 0, (int)position);
            }
        }

        public void Send<T>(MessageAddress targetAddress, T value) where T : notnull, new()
        {
            var nextMessage = new TcpDeserializedMessage
            {
                SenderAddress = new MessageAddress(_guid),
                RecipientAddresses = new ArchiveArray<int, MessageAddress>(new[] { targetAddress }),
                MessageScope = MessageScope.All,
                TimeSent = DateTimeOffset.UtcNow,
                ExpirationTime = DateTimeOffset.MaxValue,
            };
            nextMessage.SetMessageData(value);

            _queuedToSend.Enqueue(nextMessage);
            _readyToSend.Release();
        }

        public void Send<T>(T value) where T : notnull, new()
        {
            var nextMessage = new TcpDeserializedMessage
            {
                SenderAddress = new MessageAddress(_guid),
                MessageScope = MessageScope.All,
                TimeSent = DateTimeOffset.UtcNow,
                ExpirationTime = DateTimeOffset.MaxValue,
            };
            nextMessage.SetMessageData(value);

            _queuedToSend.Enqueue(nextMessage);
            _readyToSend.Release();
        }

        public void Respond<T>(TcpDeserializedMessage sourceMessage, T value) where T : notnull, new()
        {
            var nextMessage = new TcpDeserializedMessage
            {
                SenderAddress = new MessageAddress(_guid),
                RecipientAddresses = new ArchiveArray<int, MessageAddress>(new MessageAddress[] { sourceMessage.SenderAddress }),
                MessageScope = MessageScope.All,
                TimeSent = DateTimeOffset.UtcNow,
                ExpirationTime = DateTimeOffset.MaxValue,
            };
            nextMessage.SetMessageData(value);

            _queuedToSend.Enqueue(nextMessage);
            _readyToSend.Release();
        }

        public void WriteConsole(Action consoleWrite)
        {
            lock (_consoleLock)
            {
                consoleWrite();
            }
        }

        public void ReceiveUntil(Func<TcpDeserializedMessage, bool> onMessageReceived, CancellationToken cancellationToken)
        {
            var receiveArchive = new Archive(_client.GetStream(), true);

            if (!_receivedHeader)
            {
                var header = new TcpMessageHeader();
                receiveArchive.Serialize(ref header);
                _remoteNodeId = header.NodeId;
                _receivedHeader = true;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                uint nextBytes = 0;
                receiveArchive.Serialize(ref nextBytes);

                var buffer = new byte[0];
                receiveArchive.Serialize(ref buffer, nextBytes);

                var nextMessage = new TcpDeserializedMessage();
                try
                {
                    using (var memory = new MemoryStream(buffer))
                    {
                        var memoryArchive = new Archive(memory, true);
                        memoryArchive.Serialize(ref nextMessage);
                    }
                }
                catch (TopLevelAssetPathNotFoundException)
                {
                    if (!_silent)
                    {
                        lock (_consoleLock)
                        {
                            var oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.Write($"{nextMessage.TimeSent}");
                            Console.ForegroundColor = oldColor;
                            Console.WriteLine($" {{{nextMessage.SenderAddress.UniqueId}}} -> {{{(nextMessage.RecipientAddresses.Data.Length > 0 ? nextMessage.RecipientAddresses.Data[0].UniqueId : "*")}}} [{nextMessage.AssetPath.PackageName + "." + nextMessage.AssetPath.AssetName}]\n(no C# class registered for this message type)");
                        }
                    }
                    continue;
                }

                {
                    if (!_silent)
                    {
                        lock (_consoleLock)
                        {
                            var oldColor = Console.ForegroundColor;
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.Write($"{nextMessage.TimeSent}");
                            Console.ForegroundColor = oldColor;
                            Console.WriteLine($" {{{nextMessage.SenderAddress.UniqueId}}} -> {{{(nextMessage.RecipientAddresses.Data.Length > 0 ? nextMessage.RecipientAddresses.Data[0].UniqueId : "*")}}} [{nextMessage.AssetPath.PackageName + "." + nextMessage.AssetPath.AssetName}]\n{nextMessage.GetMessageData()}");
                        }
                    }

                    if (onMessageReceived(nextMessage))
                    {
                        return;
                    }
                }
            }
        }

        public void Dispose()
        {
            _disposed = true;
            ((IDisposable)_client).Dispose();
        }
    }
}
