namespace Redpoint.KubernetesManager.Models.Hns
{
    using System.Text.Json.Serialization;

    internal class HnsResponse
    {
        [JsonPropertyName("Success")]
        public bool Success { get; set; }
    }

    internal class HnsResponse<T> : HnsResponse
    {
        [JsonPropertyName("Output")]
        public T Output { get; set; } = default!;
    }
}
