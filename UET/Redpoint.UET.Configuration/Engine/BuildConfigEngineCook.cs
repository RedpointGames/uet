namespace Redpoint.UET.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigEngineCook
    {
        [JsonPropertyName("GenerateDDC")]
        public bool GenerateDDC { get; set; } = false;
    }
}
