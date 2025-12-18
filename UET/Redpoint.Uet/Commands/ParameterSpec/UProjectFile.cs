namespace UET.Commands.EngineSpec
{
    using System.Text.Json.Serialization;

    internal sealed class UProjectFile
    {
        [JsonPropertyName("EngineAssociation")]
        public string? EngineAssociation { get; set; }
    }
}
