namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal class GitHubUpdateRef
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }

        [JsonPropertyName("force")]
        public bool? Force { get; set; }
    }
}
