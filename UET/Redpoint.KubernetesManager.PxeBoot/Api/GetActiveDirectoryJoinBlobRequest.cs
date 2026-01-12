namespace Redpoint.KubernetesManager.PxeBoot.Api
{
    using System.Text.Json.Serialization;

    internal class GetActiveDirectoryJoinBlobRequest
    {
        [JsonPropertyName("nodeName")]
        public required string NodeName { get; set; }

        [JsonPropertyName("asUnattendXml")]
        public required bool AsUnattendXml { get; set; }
    }
}
