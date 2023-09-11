namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Custom
{
    using Redpoint.Uet.Configuration;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigPluginTestCustom
    {
        /// <summary>
        /// Must be set to either "TestProject" or "PackagedPlugin".
        /// </summary>
        [JsonPropertyName("TestAgainst"), JsonRequired]
        public BuildConfigPluginTestCustomTestAgainst TestAgainst { get; set; } = BuildConfigPluginTestCustomTestAgainst.TestProject;

        /// <summary>
        /// A list of platforms to execute custom tests on. A set of "Win64", "Mac" or both.
        /// </summary>
        [JsonPropertyName("Platforms"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigHostPlatform[] Platforms { get; set; } = Array.Empty<BuildConfigHostPlatform>();

        /// <summary>
        /// The path to the test script, relative to the repository root.
        /// If you are testing against "TestProject", then the script receives the following parameters:
        ///   -EnginePath "C:\Path\To\UnrealEngine"
        ///   -TestProjectPath "C:\Path\To\UProject\File.uproject"
        /// If you are testing against "PackagedPlugin", then the script receives the following parameters:
        ///   -EnginePath "C:\Path\To\UnrealEngine"
        ///   -TempPath "C:\Path\To\TemporaryFolder"
        ///   -PackagedPluginPath "C:\Path\To\PackagedPlugin"
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;
    }
}
