namespace Redpoint.KubernetesManager.PxeBoot.Provisioning.Step.ExecuteProcess
{
    using System.Text.Json.Serialization;

    internal class ExecuteProcessProvisioningStepConfig
    {
        [JsonPropertyName("executable")]
        public string Executable { get; set; } = string.Empty;

        [JsonPropertyName("search")]
        public bool Search { get; set; } = true;

        [JsonPropertyName("arguments")]
        public string[] Arguments { get; set; } = [];

        [JsonPropertyName("workingDirectory")]
        public string? WorkingDirectory { get; set; }

        [JsonPropertyName("ignoreExitCode")]
        public bool IgnoreExitCode { get; set; } = false;

        [JsonPropertyName("environmentVariables")]
        public Dictionary<string, string> EnvironmentVariables { get; set; } = new();

        [JsonPropertyName("inheritEnvironmentVariables")]
        public bool InheritEnvironmentVariables { get; set; } = true;

        [JsonPropertyName("monitorLogDirectory")]
        public string? MonitorLogDirectory { get; set; }

        [JsonPropertyName("script")]
        public string? Script { get; set; }

        public ExecuteProcessProvisioningStepConfig Clone()
        {
            return new ExecuteProcessProvisioningStepConfig
            {
                Executable = Executable,
                Search = Search,
                Arguments = Arguments,
                WorkingDirectory = WorkingDirectory,
                IgnoreExitCode = IgnoreExitCode,
                EnvironmentVariables = new(EnvironmentVariables),
                InheritEnvironmentVariables = InheritEnvironmentVariables,
                MonitorLogDirectory = MonitorLogDirectory,
                Script = Script,
            };
        }
    }
}
