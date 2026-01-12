namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class GetActiveDirectoryJoinBlobResponse
    {
        [JsonPropertyName("joinBlob")]
        public required string JoinBlob { get; set; }
    }
}
