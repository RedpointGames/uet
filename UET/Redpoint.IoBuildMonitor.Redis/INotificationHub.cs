namespace Io.Redis
{
    public interface INotificationHub
    {
        Task NotifyAsync(NotificationType type);

        Task<long> RegisterForNotifyChanges(Func<NotificationType, Task> handler);

        Task UnregisterForNotifyChanges(long handle);
    }
}