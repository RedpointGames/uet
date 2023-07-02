namespace Redpoint.Uet.BuildPipeline.Providers.Test.Project.Custom
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectTestCustom
    {
        /// <summary>
        /// The path to the test script, relative to the repository root.
        /// The script receives the following parameters:
        ///   -EnginePath "C:\Path\To\UnrealEngine"
        ///   -StageDirectory "C:\Path\To\UProject\Saved\StagedBuilds"
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;
    }
}
