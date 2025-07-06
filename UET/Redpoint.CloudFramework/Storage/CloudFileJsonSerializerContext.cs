namespace Redpoint.CloudFramework.Storage
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(CloudFile))]
    [JsonSerializable(typeof(CloudFile[]))]
    public partial class CloudFileJsonSerializerContext : JsonSerializerContext
    {
    }
}
