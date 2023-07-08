namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using System.Text.Json.Serialization;

    internal class BuildConfigProjectPrepareDownloadPlugin
    {
        /// <summary>
        /// An ordered list of sources that the plugin should be obtained from. Only the
        /// first working source will be used.
        /// </summary>
        [JsonPropertyName("Sources"), JsonRequired]
        public BuildConfigProjectPrepareDownloadPluginSource[]? Sources { get; set; }

        /// <summary>
        /// The name of the folder underneath Plugins where this plugin will be placed.
        /// </summary>
        [JsonPropertyName("FolderName"), JsonRequired]
        public string? FolderName { get; set; }
    }
}
