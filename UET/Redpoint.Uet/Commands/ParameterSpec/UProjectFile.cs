namespace Redpoint.Uet.Commands.ParameterSpec
{
    using System.Text.Json.Serialization;

    internal sealed class UProjectFile
    {
        [JsonPropertyName("EngineAssociation")]
        public string? EngineAssociation { get; set; }
    }
}
