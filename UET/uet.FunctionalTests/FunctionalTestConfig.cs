namespace uet.FunctionalTests
{
    using System.Text.Json.Serialization;

    public class FunctionalTestConfig
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Type")]
        public string? Type { get; set; }

        [JsonPropertyName("Arguments")]
        public string[]? Arguments { get; set; }
    }
}
