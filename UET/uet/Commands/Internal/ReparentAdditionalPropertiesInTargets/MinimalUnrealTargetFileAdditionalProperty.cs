namespace UET.Commands.Internal.ReparentAdditionalPropertiesInTargets
{
    using System.Text.Json.Serialization;

    internal class MinimalUnrealTargetFileAdditionalProperty
    {
        [JsonPropertyName("Name")]
        public string? Name { get; set; }

        [JsonPropertyName("Value")]
        public string? Value { get; set; }
    }
}
