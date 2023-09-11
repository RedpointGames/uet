namespace UET.Commands.Build
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(UPluginFile))]
    internal partial sealed class ProjectPluginFileJsonSerializerContext : JsonSerializerContext
    {

    }
}
