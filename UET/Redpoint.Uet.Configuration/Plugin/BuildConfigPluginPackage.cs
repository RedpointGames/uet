namespace Redpoint.Uet.Configuration.Plugin
{
    using System.Text.Json.Serialization;

    public class BuildConfigPluginPackage
    {
        /// <summary>
        /// Defines the package type, such as whether it is being packaged for Marketplace or Fab submission. One of 'None', 'Generic', 'Marketplace' or 'Fab'. If not set, defaults to 'None'.
        /// </summary>
        [JsonPropertyName("Type"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public BuildConfigPluginPackageType? Type { get; set; }

        /// <summary>
        /// DEPRECATED. Use the 'Type' setting instead.
        /// </summary>
        [JsonPropertyName("Marketplace"), Obsolete("Use the 'Type' attribute instead."), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? Marketplace { get; set; }

        /// <summary>
        /// If not set, defaults to "Packaged".
        /// </summary>
        [JsonPropertyName("OutputFolderName"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OutputFolderName { get; set; }

        /// <summary>
        /// The path to the FilterPlugin.ini file used for packaging.
        /// </summary>
        [JsonPropertyName("Filter"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Filter { get; set; }

        /// <summary>
        /// If set, overrides the default as to whether PRECOMPILED REMOVE BEGIN -> PRECOMPILED REMOVE END content should be removed from Build.cs files.
        /// 
        /// By default, if the package type is 'Generic', Build.cs files will have lines between PRECOMPILED REMOVE BEGIN and 
        /// PRECOMPILED REMOVE END comments removed. For example:
        /// 
        /// /* PRECOMPILED REMOVE BEGIN */
        /// var bIncludesSourceCode = true;
        /// if (!bIncludesSourceCode)
        /// {
        ///     /* PRECOMPILED REMOVE END */
        ///     bUsePrecompiled = true;
        ///     /* PRECOMPILED REMOVE BEGIN */
        /// }
        /// /* PRECOMPILED REMOVE END */
        /// </summary>
        [JsonPropertyName("UsePrecompiled"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? UsePrecompiled { get; set; }
    }
}
