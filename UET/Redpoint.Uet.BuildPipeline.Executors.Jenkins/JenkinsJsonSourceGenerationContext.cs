namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using System.Text.Json.Serialization;

    [JsonSourceGenerationOptions(WriteIndented = true)]
    [JsonSerializable(typeof(JenkinsQueueItem))]
    internal partial class JenkinsJsonSourceGenerationContext : JsonSerializerContext
    {
    }
}
