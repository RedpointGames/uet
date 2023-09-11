namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class BranchResponse
    {
        [JsonPropertyName("commit")]
        public CommitPointer? Commit { get; set; }
    }
}
