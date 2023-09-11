namespace Redpoint.Uefs.Daemon.PackageFs.Tagging
{
    using System.Text.Json.Serialization;

    internal sealed class PackageStorageTag
    {
        /// <summary>
        /// The full tag. The filename of the .tag file that this is stored in is
        /// the SHA-256 of this value.
        /// </summary>
        [JsonPropertyName("url")]
        public string Tag { get; set; } = string.Empty;

        /// <summary>
        /// The hash of the data file. This will be something like sha256_4f6...a05.
        /// For local storage, the VHD or sparse image is named "hash.vhd".
        /// </summary>
        [JsonPropertyName("hash")]
        public string Hash { get; set; } = string.Empty;
    }
}
