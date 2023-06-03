namespace Redpoint.UET.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectTest
    {
        /// <summary>
        /// The name of the job/step as it would be displayed on a build server. This must be unique amongst all tests defined.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the test type.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigProjectTestType Type { get; set; }

        /// <summary>
        /// Specifies the settings for executing Gauntlet tests.
        /// </summary>
        [JsonPropertyName("Gauntlet"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectTestGauntlet? Gauntlet { get; set; }

        /// <summary>
        /// Specifies the settings for executing custom tests.
        /// </summary>
        [JsonPropertyName("Custom"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectTestCustom? Custom { get; set; }
    }
}
