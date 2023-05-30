namespace Redpoint.UET.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectPrepare
    {
        /// <summary>
        /// Currently only "Custom" is supported.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigProjectPrepareType Type { get; set; }

        /// <summary>
        /// The types of steps that the preparation script should run before. A set of "AssembleFinalize", "Compile", "Test" and "BuildGraph".
        /// </summary>
        [JsonPropertyName("RunBefore"), JsonRequired]
        public BuildConfigProjectPrepareRunBefore[] RunBefore { get; set; } = new BuildConfigProjectPrepareRunBefore[0];

        /// <summary>
        /// The parameters for a custom preparation step.
        /// </summary>
        [JsonPropertyName("Custom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectPrepareCustom? Custom { get; set; }
    }
}
