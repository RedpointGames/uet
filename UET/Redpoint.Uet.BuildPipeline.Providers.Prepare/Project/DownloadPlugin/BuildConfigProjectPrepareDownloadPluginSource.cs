namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public sealed class BuildConfigProjectPrepareDownloadPluginSource
    {
        /// <summary>
        /// The Git URL to clone the plugin from. Environment variables can be specified
        /// in the form ${ENV_VAR} and will be replaced. This source will only be selected
        /// if all the environment variables used in the URL are set.
        /// </summary>
        [JsonPropertyName("GitUrl"), JsonRequired]
        [SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "This is a JSON property.")]
        public string? GitUrl { get; set; }

        /// <summary>
        /// The Git ref to checkout. This can be a branch name.
        /// </summary>
        [JsonPropertyName("GitRef"), JsonRequired]
        public string? GitRef { get; set; }

        // @note: When we support more than Git URL here, remove the [JsonRequired]
        // attributes from the property above.
    }
}
