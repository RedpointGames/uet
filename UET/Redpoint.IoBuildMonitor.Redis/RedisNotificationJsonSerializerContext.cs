namespace Io.Redis
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(RedisNotificationEvent))]
    internal partial class RedisNotificationJsonSerializerContext : JsonSerializerContext
    {
    }
}