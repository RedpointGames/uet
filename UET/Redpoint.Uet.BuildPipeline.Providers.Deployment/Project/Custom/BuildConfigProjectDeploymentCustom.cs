namespace Redpoint.Uet.BuildPipeline.Providers.Deployment.Project.Custom
{
    using Package;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigProjectDeploymentCustom
    {
        /// <summary>
        /// The path to the custom deployment script.
        /// The script receives the following parameters:
        ///   -EnginePath "C:\Path\To\UnrealEngine"
        ///   -StageDirectory "C:\Path\To\UProject\Saved\StagedBuilds"
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;

        /// <summary>
        /// Specifies the packaging targets to obtain for deployment.
        /// If not specified, all targets are obtained.
        /// </summary>
        [JsonPropertyName("Packages")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigProjectDeploymentPackage[]? Packages { get; set; }
    }
}
