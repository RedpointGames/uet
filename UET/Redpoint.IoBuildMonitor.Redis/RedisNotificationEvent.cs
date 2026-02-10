namespace Io.Redis
{
    public class RedisNotificationEvent
    {
        public string T { get; set; } = string.Empty;

        public NotificationType Y { get; set; }
    }
}