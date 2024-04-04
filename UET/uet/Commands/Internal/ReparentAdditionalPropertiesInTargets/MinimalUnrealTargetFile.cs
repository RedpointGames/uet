namespace UET.Commands.Internal.ReparentAdditionalPropertiesInTargets
{
    using System.Text.Json.Serialization;

    internal class MinimalUnrealTargetFile
    {
        [JsonPropertyName("AdditionalProperties")]
        public List<MinimalUnrealTargetFileAdditionalProperty>? AdditionalProperties { get; set; }
    }
}
