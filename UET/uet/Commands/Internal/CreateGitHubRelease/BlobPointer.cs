namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal class BlobPointer
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
