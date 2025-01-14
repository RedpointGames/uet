namespace Redpoint.Uet.BuildPipeline.Executors.Jenkins
{
    using System.Text.Json.Serialization;

    internal class JenkinsQueueItem
    {
        internal class JenkinsExecutable
        {
            [JsonPropertyName("url"), JsonRequired]
            public string Url { get; set; } = string.Empty;
        }

        [JsonPropertyName("cancelled")]
        public bool? Cancelled { get; set; }

        [JsonPropertyName("executable")]
        public JenkinsExecutable? Executable { get; set; }
    }
}
