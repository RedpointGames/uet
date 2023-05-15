namespace BuildRunner.Configuration.Engine
{
    using System.Text.Json.Serialization;

    internal class BuildConfigEngineCook
    {
        [JsonPropertyName("GenerateDDC")]
        public bool GenerateDDC { get; set; } = false;
    }
}
