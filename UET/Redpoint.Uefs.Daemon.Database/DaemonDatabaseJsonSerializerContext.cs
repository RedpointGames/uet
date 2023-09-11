namespace Redpoint.Uefs.Daemon.Database
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(DaemonDatabase))]
    [JsonSerializable(typeof(DaemonDatabasePersistentMount))]
    public partial class DaemonDatabaseJsonSerializerContext : JsonSerializerContext
    {
        public static DaemonDatabaseJsonSerializerContext WithStringEnums { get; } = new(new JsonSerializerOptions
        {
            Converters =
            {
                new JsonStringEnumConverter(),
            }
        });
    }
}
