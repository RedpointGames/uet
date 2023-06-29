namespace UET.Commands.EngineSpec
{
    using System.Text.Json.Serialization;

    internal class UProjectFile
    {
        [JsonPropertyName("EngineAssociation")]
        public string? EngineAssociation { get; set; }
    }
}
