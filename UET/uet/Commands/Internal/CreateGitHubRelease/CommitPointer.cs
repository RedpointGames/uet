namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal sealed class CommitPointer
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
