namespace UET.Commands.Internal.RegisterGitLabRunner
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(GitLabRunnerRegistrationSpec[]))]
    [JsonSerializable(typeof(GitLabGetRunnerResponse))]
    [JsonSerializable(typeof(GitLabRegisterRunnerResponse))]
    internal partial class RegisterGitLabRunnerJsonSerializerContext : JsonSerializerContext
    {
    }
}
