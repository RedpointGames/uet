namespace UET.Commands.Internal.CreateGitHubRelease
{
    using System.Text.Json.Serialization;

    [JsonSerializable(typeof(GitHubNewRelease))]
    [JsonSerializable(typeof(GitHubPatchAsset))]
    [JsonSerializable(typeof(ReleaseResponse))]
    [JsonSerializable(typeof(AssetResponse[]))]
    [JsonSerializable(typeof(BranchResponse))]
    [JsonSerializable(typeof(TreeResponse))]
    [JsonSerializable(typeof(GitHubNewBlob))]
    [JsonSerializable(typeof(BlobPointer))]
    [JsonSerializable(typeof(CommitResponse))]
    [JsonSerializable(typeof(GitHubNewTree))]
    [JsonSerializable(typeof(GitHubNewCommit))]
    [JsonSerializable(typeof(GitHubUpdateRef))]
    internal sealed partial class GitHubJsonSerializerContext : JsonSerializerContext
    {
    }
}
