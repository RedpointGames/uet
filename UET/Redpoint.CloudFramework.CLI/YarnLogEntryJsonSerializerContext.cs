namespace Redpoint.CloudFramework.CLI
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(YarnLogEntry))]
    internal partial class YarnLogEntryJsonSerializerContext : JsonSerializerContext
    {
    }
}
