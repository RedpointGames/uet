namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(JenkinsQueueItem))]
    [JsonSerializable(typeof(JenkinsBuildInfo))]
    internal partial class JenkinsJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}
