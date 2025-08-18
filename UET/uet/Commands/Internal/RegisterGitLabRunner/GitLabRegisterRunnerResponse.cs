namespace UET.Commands.Internal.RegisterGitLabRunner
{
    using System.Text.Json.Serialization;

    internal class GitLabRegisterRunnerResponse
    {
        [JsonPropertyName("id"), JsonRequired]
        public required int Id { get; set; }

        [JsonPropertyName("token"), JsonRequired]
        public required string Token { get; set; }
    }
}
