namespace Redpoint.UET.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginGauntlet
    {
        /// <summary>
        /// Configuration files to apply to the project that hosts Gauntlet tests.
        /// </summary>
        [JsonPropertyName("ConfigFiles"), JsonRequired]
        public string[]? ConfigFiles { get; set; } = null;
    }
}
