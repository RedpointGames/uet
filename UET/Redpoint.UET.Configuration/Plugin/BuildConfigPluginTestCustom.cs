namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginTestCustom
    {
        /// <summary>
        /// Must be set to either "TestProject" or "PackagedPlugin".
        /// </summary>
        [JsonPropertyName("TestAgainst"), JsonRequired]
        public string TestAgainst { get; set; } = string.Empty;

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
