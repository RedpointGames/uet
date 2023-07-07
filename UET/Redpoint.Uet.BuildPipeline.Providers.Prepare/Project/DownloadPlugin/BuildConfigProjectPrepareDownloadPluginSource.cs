namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using System.Text.Json.Serialization;

    internal class BuildConfigProjectPrepareDownloadPluginSource
    {
        /// <summary>
        /// The Git URL to clone the plugin from. Environment variables can be specified
        /// in the form ${ENV_VAR} and will be replaced. This source will only be selected
        /// if all the environment variables used in the URL are set.
        /// </summary>
        [JsonPropertyName("GitUrl"), JsonRequired]
        public string? GitUrl { get; set; }

        // @note: When we support more than Git URL here, remove the [JsonRequired]
        // attributes from the property above.
    }
}
