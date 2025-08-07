using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace UET.Commands.Cluster
{
    public class SourceVipResponse
    {
        [JsonPropertyName("cniVersion")]
        public required string CniVersion { get; set; }

        [JsonPropertyName("ips")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "Used for JSON serialization.")]
        public required SourceVipResponseIp[] IPs { get; set; }
    }
}
