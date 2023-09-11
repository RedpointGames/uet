namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class GitHubNewTree
    {
        [JsonPropertyName("base_tree")]
        public string? BaseTree { get; set; }

        [JsonPropertyName("tree")]
        public List<TreeEntry>? Tree { get; set; }
    }
}
