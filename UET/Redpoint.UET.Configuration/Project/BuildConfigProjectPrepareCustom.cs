namespace Redpoint.UET.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectPrepareCustom
    {
        /// <summary>
        /// The path to the PowerShell script to execute, relative to the repository root.
        /// </summary>
        [JsonPropertyName("ScriptPath"), JsonRequired]
        public string ScriptPath { get; set; } = string.Empty;
    }
}
