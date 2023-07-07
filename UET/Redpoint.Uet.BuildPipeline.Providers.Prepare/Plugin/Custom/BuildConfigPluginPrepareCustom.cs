namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Plugin.Custom
{
    using System.Text.Json.Serialization;

    internal class BuildConfigPluginPrepareCustom
    {
        /// <summary>
        /// The path to the PowerShell script to execute, relative to the repository root.
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// When to run this preparation step.
        /// </summary>
        [JsonPropertyName("RunBefore"), JsonRequired]
        public BuildConfigPluginPrepareRunBefore[]? RunBefore { get; set; }
    }
}
