namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class TrueNasDataset
    {
        [JsonPropertyName("pool")]
        public required string Pool { get; set; }

        [JsonPropertyName("name")]
        public required string Name { get; set; }

        [JsonPropertyName("id")]
        public required string Id { get; set; }

        [JsonPropertyName("children")]
        public required TrueNasDataset[] Children { get; set; }
    }
}
