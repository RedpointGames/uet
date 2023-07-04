namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    internal class TreePointer
    {
        [JsonPropertyName("sha")]
        public string? Sha { get; set; }
    }
}
