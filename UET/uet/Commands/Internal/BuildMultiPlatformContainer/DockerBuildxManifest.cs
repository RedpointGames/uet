namespace UET.Commands.Internal.BuildMultiPlatformContainer
{
    using System.Text.Json.Serialization;

    internal class DockerBuildxManifest
    {
        [JsonPropertyName("containerimage.digest")]
        public string Digest { get; set; } = string.Empty;
    }
}
