namespace UET.Commands.Build
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(UPluginFile))]
    internal sealed partial class ProjectPluginFileJsonSerializerContext : JsonSerializerContext
    {

    }
}
