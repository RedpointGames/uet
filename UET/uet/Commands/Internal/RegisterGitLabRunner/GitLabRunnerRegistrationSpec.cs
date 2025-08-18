namespace UET.Commands.Internal.RegisterGitLabRunner
{
    using System.Text.Json.Serialization;

    internal class GitLabRunnerRegistrationSpec : GitLabRunnerSpec
    {
        [JsonPropertyName("id_path"), JsonRequired]
        public required string IdPath { get; set; }

        [JsonPropertyName("token_path"), JsonRequired]
        public required string TokenPath { get; set; }
    }
}
