namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.ProjectPackage
{
    using Redpoint.Uet.Configuration;
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Specifies how to package and test a project against this plugin.
    /// </summary>
    public class BuildConfigPluginTestProjectPackage
    {
        /// <summary>
        /// The host platform that the project is built on.
        /// </summary>
        [JsonPropertyName("HostPlatform"), JsonRequired]
        public BuildConfigHostPlatform HostPlatform { get; set; }

        /// <summary>
        /// If specified, the path underneath the plugin root to copy files from into the project after obtaining it from the source. This can be used to set config files for the project.
        /// </summary>
        [JsonPropertyName("ProjectCopyFilesPath")]
        public string? ProjectCopyFilesPath { get; set; } = null;

        /// <summary>
        /// The platform to build, cook and package for.
        /// </summary>
        [JsonPropertyName("TargetPlatform"), JsonRequired]
        public string TargetPlatform { get; set; } = string.Empty;

        /// <summary>
        /// The editor target to compile. If not set, defaults to "UnrealEditor".
        /// </summary>
        [JsonPropertyName("CompileEditorTarget")]
        public string? CompileEditorTarget { get; set; } = null;

        /// <summary>
        /// If set, UET will run Gauntlet to ensure the packaged project boots on device.
        /// </summary>
        [JsonPropertyName("BootTest")]
        public BuildConfigPluginTestProjectPackageBootTest? BootTest { get; set; } = null;
    }
}
