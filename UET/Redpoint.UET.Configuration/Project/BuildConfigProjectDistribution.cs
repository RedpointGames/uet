namespace Redpoint.UET.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDistribution
    {
        /// <summary>
        /// The name, as passed to the --distribution argument.
        /// </summary>
        [JsonPropertyName("Name"), JsonRequired]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The relative path that the .uproject file is located underneath.
        /// </summary>
        [JsonPropertyName("FolderName"), JsonRequired]
        public string FolderName { get; set; } = string.Empty;

        /// <summary>
        /// The name of the .uproject file underneath the folder, without the .uproject extension.
        /// </summary>
        [JsonPropertyName("ProjectName"), JsonRequired]
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the preparation scripts to run before various steps. You can specify multiple preparation entries.
        /// </summary>
        [JsonPropertyName("Prepare"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigProjectPrepare[]? Prepare { get; set; }
    }
}
