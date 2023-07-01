namespace Redpoint.Uefs.ContainerRegistry
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Represents a credential used to authenticate with a Docker registry.
    /// </summary>
    public class RegistryCredential
    {
        /// <summary>
        /// The username to authenticate with.
        /// </summary>
        [JsonPropertyName("Username")]
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// The password to authenticate with.
        /// </summary>
        [JsonPropertyName("Password")]
        public string Password { get; set; } = string.Empty;
    }
}
