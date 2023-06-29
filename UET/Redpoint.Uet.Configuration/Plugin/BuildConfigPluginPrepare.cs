namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginPrepare
    {
        /// <summary>
        /// Currently only "Custom" is supported.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigPluginPrepareType Type { get; set; }

        /// <summary>
        /// The types of steps that the preparation script should run before. A set of "AssembleFinalize", "Compile", "Test" and "BuildGraph".
        /// </summary>
        [JsonPropertyName("RunBefore"), JsonRequired]
        public BuildConfigPluginPrepareRunBefore[] RunBefore { get; set; } = new BuildConfigPluginPrepareRunBefore[0];

        /// <summary>
        /// The parameters for a custom preparation step.
        /// </summary>
        [JsonPropertyName("Custom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginPrepareCustom? Custom { get; set; }
    }
}
