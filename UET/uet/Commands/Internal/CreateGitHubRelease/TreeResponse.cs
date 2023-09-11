namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class TreeResponse
    {
        [JsonPropertyName("tree")]
        public List<TreeEntry>? Tree { get; set; }
    }
}
