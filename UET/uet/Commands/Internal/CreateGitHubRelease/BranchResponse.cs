namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal class BranchResponse
    {
        [JsonPropertyName("commit")]
        public CommitPointer? Commit { get; set; }
    }
}
