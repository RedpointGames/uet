namespace UET.Commands.Build
{
    using System.Text.Json.Serialization;

    internal class UPluginFile
    {
        [JsonPropertyName("CreatedBy")]
        public string? CreatedBy { get; set; }
    }
}
