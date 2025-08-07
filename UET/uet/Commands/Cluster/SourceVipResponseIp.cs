using System.Text.Json.Serialization;

namespace UET.Commands.Cluster
{
    public class SourceVipResponseIp
    {
        [JsonPropertyName("address")]
        public required string Address { get; set; }

        [JsonPropertyName("gateway")]
        public required string Gateway { get; set; }
    }
}
