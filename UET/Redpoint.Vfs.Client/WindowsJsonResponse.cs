namespace Redpoint.Vfs.Client
{
    using System.Text.Json.Serialization;

    internal class WindowsJsonResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }
    }
}
