namespace Redpoint.Uet.BuildPipeline.Providers.Prepare.Project.DownloadPlugin
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public sealed class BuildConfigProjectPrepareDownloadPlugin
    {
        /// <summary>
        /// An ordered list of sources that the plugin should be obtained from. Only the
        /// first working source will be used.
        /// </summary>
        [JsonPropertyName("Sources"), JsonRequired]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public BuildConfigProjectPrepareDownloadPluginSource[]? Sources { get; set; }

        /// <summary>
        /// The name of the folder underneath Plugins where this plugin will be placed.
        /// </summary>
        [JsonPropertyName("FolderName"), JsonRequired]
        public string? FolderName { get; set; }
    }
}
