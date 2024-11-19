namespace Redpoint.Uet.Configuration.Project
{
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
    }
}
