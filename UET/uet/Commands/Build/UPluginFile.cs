namespace UET.Commands.Build
{
    using System.Text.Json.Serialization;

    internal sealed class UPluginFile
    {
        [JsonPropertyName("CreatedBy")]
        public string? CreatedBy { get; set; }
    }
}
