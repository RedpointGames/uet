namespace Redpoint.Uet.Configuration.Project
{
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies how to build, cook, test and deploy an Unreal Engine project.
    /// </summary>
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
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigDynamic<BuildConfigProjectDistribution, IPrepareProvider>[]? Prepare { get; set; }

        /// <summary>
        /// Specifies how to build, cook and stage the project.
        /// </summary>
        [JsonPropertyName("Build")]
        public BuildConfigProjectBuild Build { get; set; } = new BuildConfigProjectBuild();

        /// <summary>
        /// A list of tests to run for the project.
        /// </summary>
        [JsonPropertyName("Tests"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigDynamic<BuildConfigProjectDistribution, ITestProvider>[]? Tests { get; set; }

        /// <summary>
        /// A list of deployments to run for the project.
        /// </summary>
        [JsonPropertyName("Deployments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigDynamic<BuildConfigProjectDistribution, IDeploymentProvider>[]? Deployments { get; set; }
    }
}
