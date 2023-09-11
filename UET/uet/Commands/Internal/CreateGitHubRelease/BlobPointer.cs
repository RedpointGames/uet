namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class BlobPointer
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
