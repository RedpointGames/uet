namespace Redpoint.Uet.Configuration
{
    using System.Diagnostics.CodeAnalysis;
    using System.Text.Json.Serialization;

    public class BuildConfigTargetPlatform
    {
        /// <summary>
        /// The platform name to build the project for.
        /// </summary>
        [JsonPropertyName("Platform")]
        public string Platform { get; set; } = string.Empty;

        /// <summary>
        /// The flavors to cook an Android platform for. e.g. ASTC, DXT, ETC2, Multi.
        /// </summary>
        [JsonPropertyName("CookFlavors")]
        [SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "This property is used for JSON serialization.")]
        public string[]? CookFlavors { get; set; } = null;
    }
}
