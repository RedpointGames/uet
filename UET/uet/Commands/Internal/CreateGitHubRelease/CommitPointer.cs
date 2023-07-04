namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal class CommitPointer
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
