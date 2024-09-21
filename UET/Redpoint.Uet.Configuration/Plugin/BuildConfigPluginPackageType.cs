namespace Redpoint.Uet.Configuration.Plugin
{
    /// <remarks>
    /// The names of these enumeration values can not be changed without also updating the plugin BuildGraph XML.
    /// </remarks>
    public enum BuildConfigPluginPackageType
    {
        /// <summary>
        /// The plugin is being packaged for a generic store or self-hosted distribution. The plugin package will contain binary files.
        /// </summary>
        Generic,

        /// <summary>
        /// The plugin is being packaged for submission to the Unreal Engine Marketplace. The plugin will not contain binary files.
        /// </summary>
        Marketplace,

        /// <summary>
        /// The plugin is being packaged for submission to Fab. The plugin will not contain binary files.
        /// </summary>
        Fab
    }
}
