using System.Text.Json.Serialization;

namespace uefs.Registry
{
    /// <summary>
    /// A reference (stored in a Docker registry) that points to a file on a network share, with the specified hash.
    /// </summary>
    public class RegistryReferenceInfo
    {
        /// <summary>
        /// The absolute path to the file on the network share.
        /// </summary>
        [JsonPropertyName("location")]
        public string Location { get; set; } = string.Empty;

        /// <summary>
        /// The SHA256 hash of the file on the network share.
        /// </summary>
        [JsonPropertyName("digest")]
        public string Digest { get; set; } = string.Empty;
    }
}
