namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using System.Text.Json.Serialization;

    internal class JenkinsBuildInfo
    {
        [JsonPropertyName("result"), JsonRequired]
        public string Result { get; set; } = string.Empty;
    }
}
