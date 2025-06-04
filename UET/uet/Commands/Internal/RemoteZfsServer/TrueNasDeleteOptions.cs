namespace UET.Commands.Internal.RemoteZfsServer
{
    using System.Text.Json.Serialization;

    internal class TrueNasDeleteOptions
    {
        [JsonPropertyName("recursive")]
        public bool Recursive { get; set; }

        [JsonPropertyName("force")]
        public bool Force { get; set; }
    }
}
