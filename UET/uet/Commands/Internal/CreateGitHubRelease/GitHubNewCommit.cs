namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class GitHubNewCommit
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("tree")]
        public string? Tree { get; set; }

        [JsonPropertyName("parents")]
        public string[]? Parents { get; set; }
    }
}
