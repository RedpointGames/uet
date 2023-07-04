namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal class GitHubNewBlob
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("encoding")]
        public string? Encoding { get; set; }
    }
}
