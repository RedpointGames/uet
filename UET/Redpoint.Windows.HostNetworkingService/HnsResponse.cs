namespace Redpoint.Windows.HostNetworkingService
{
    using System.Text.Json.Serialization;

    public class HnsResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }
    }

    public class HnsResponse<T> : HnsResponse
    {
        [JsonPropertyName("Output")]
        public T Output { get; set; } = default!;
    }
}
