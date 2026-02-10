namespace Io.Redis
{
    using StackExchange.Redis;
    using System.Globalization;
    using System.Text.Json;

    public class RedisNotificationHub : INotificationHub, IDisposable
    {
        private readonly ConnectionMultiplexer _connection;
        private Dictionary<long, Func<NotificationType, Task>> _registeredHandlers;
        private long _nextHandleId;
        private ISubscriber? _subscriber;
        private Task? _messageLoop;
        private CancellationTokenSource? _cancellationTokenSource;
        private static RedisChannel _notificationsChannel = new RedisChannel("notifications", RedisChannel.PatternMode.Literal);
        private bool _disposedValue;

        public RedisNotificationHub(ConnectionMultiplexer connection)
        {
            _connection = connection;
            _registeredHandlers = new Dictionary<long, Func<NotificationType, Task>>();
            _nextHandleId = 1000;
        }

        public async Task NotifyAsync(NotificationType type)
        {
            await _connection.GetDatabase().PublishAsync(
                _notificationsChannel,
                JsonSerializer.Serialize(
                    new RedisNotificationEvent
                    {
                        T = DateTimeOffset.UtcNow.ToString(CultureInfo.InvariantCulture),
                        Y = type,
                    },
                    RedisNotificationJsonSerializerContext.Default.RedisNotificationEvent));
        }

        public async Task<long> RegisterForNotifyChanges(Func<NotificationType, Task> handler)
        {
            if (_registeredHandlers.Count == 0 && _messageLoop == null)
            {
                _subscriber = _connection.GetSubscriber();
                var messageQueue = await _subscriber.SubscribeAsync(_notificationsChannel);
                _cancellationTokenSource = new CancellationTokenSource();
                _messageLoop = Task.Run(async () => await MessageLoop(messageQueue, _cancellationTokenSource.Token));
            }

            var handle = _nextHandleId++;
            _registeredHandlers.Add(handle, handler);
            return handle;
        }

        public async Task UnregisterForNotifyChanges(long handle)
        {
            _registeredHandlers.Remove(handle);

            if (_registeredHandlers.Count == 0 && _cancellationTokenSource != null && _messageLoop != null)
            {
                _cancellationTokenSource.Cancel();
                await _messageLoop;
            }
        }

        private async Task MessageLoop(ChannelMessageQueue messageQueue, CancellationToken cancellationToken)
        {
            while (_registeredHandlers.Count > 0 && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var nextMessage = await messageQueue.ReadAsync(cancellationToken);
                    foreach (var handler in _registeredHandlers.Values)
                    {
                        await handler(
                            JsonSerializer.Deserialize(
                                nextMessage.Message.ToString(),
                                RedisNotificationJsonSerializerContext.Default.RedisNotificationEvent)?.Y
                            ?? NotificationType.DashboardUpdated);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            await messageQueue.UnsubscribeAsync();
            _subscriber = null;
            _cancellationTokenSource = null;
            _messageLoop = null;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _connection.Dispose();
                    _cancellationTokenSource?.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}