namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class CommitResponse
    {
        [JsonPropertyName("tree")]
        public TreePointer? Tree { get; set; }
    }
}
