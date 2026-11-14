namespace Redpoint.Vfs.Client
{
    using System.Text.Json;
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(WindowsJsonRequest))]
    [JsonSerializable(typeof(WindowsJsonResponse))]
    internal partial class WindowsJsonSerializerContext : JsonSerializerContext
    {
    }
}
