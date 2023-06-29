namespace Redpoint.Uet.Configuration.Project
{
    using System.Text.Json.Serialization;

    public class BuildConfigProjectBuildEditor
    {
        /// <summary>
        /// The editor target to build.
        /// </summary>
        [JsonPropertyName("Target")]
        public string Target { get; set; } = string.Empty;
    }
}
