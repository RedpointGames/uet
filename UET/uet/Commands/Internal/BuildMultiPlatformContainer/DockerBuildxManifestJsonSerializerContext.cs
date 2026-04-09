namespace UET.Commands.Internal.BuildMultiPlatformContainer
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(DockerBuildxManifest))]
    internal partial class DockerBuildxManifestJsonSerializerContext : JsonSerializerContext
    {
    }
}
