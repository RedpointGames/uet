namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(GitHubNewRelease))]
    [JsonSerializable(typeof(GitHubPatchAsset))]
    [JsonSerializable(typeof(ReleaseResponse))]
    [JsonSerializable(typeof(AssetResponse[]))]
    internal partial class GitHubJsonSerializerContext : JsonSerializerContext
    {
    }
}
