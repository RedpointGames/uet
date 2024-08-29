namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.Custom
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public sealed class BuildConfigProjectPrepareCustom
    {
        /// <summary>
        /// The path to the PowerShell script to execute, relative to the repository root.
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// The arguments to pass to the PowerShell script, if any.
        /// </summary>
        [JsonPropertyName("ScriptArguments")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? ScriptArguments { get; set; }

        /// <summary>
        /// When to run this preparation step.
        /// </summary>
        [JsonPropertyName("RunBefore"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigProjectPrepareRunBefore[]? RunBefore { get; set; }
    }
}
