namespace Redpoint.Uet.Configuration
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a build configuration for UET, which can be used to specify how to build an Unreal Engine plugin or project, or Unreal Engine itself.
    /// </summary>
    public class BuildConfig
    {
        /// <summary>
        /// The version of UET that should be used when running against this BuildConfig.json file.
        /// </summary>
        [JsonPropertyName("UETVersion")]
        public string? UETVersion { get; set; }

        /// <summary>
        /// Specifies the type of thing that this BuildConfig.json file builds, such as whether this is for building a plugin, project or Unreal Engine itself.
        /// </summary>
        [JsonPropertyName("Type"), JsonRequired]
        public BuildConfigType Type { get; set; }
    }
}
