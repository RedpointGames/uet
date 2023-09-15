namespace UET.Commands.Internal.Runback
{
    using Redpoint.Uet.BuildPipeline.Executors.BuildServer;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    internal class RunbackJson
    {
        [JsonPropertyName("runbackId"), JsonRequired]
        public string? RunbackId { get; set; }

        [JsonPropertyName("buildJson"), JsonRequired]
        public BuildJobJson? BuildJson { get; set; }

        [JsonPropertyName("environmentVariables"), JsonRequired]
        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        [JsonPropertyName("workingDirectory"), JsonRequired]
        public string? WorkingDirectory { get; set; }
    }
}
