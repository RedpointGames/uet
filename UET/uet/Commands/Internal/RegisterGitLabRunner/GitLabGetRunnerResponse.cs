namespace UET.Commands.Internal.RegisterGitLabRunner
{
    using System.Text.Json.Serialization;

    internal class GitLabGetRunnerResponse
    {
        [JsonPropertyName("id"), JsonRequired]
        public required int Id { get; set; }
    }
}
