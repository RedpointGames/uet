namespace Redpoint.Uet.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigMobileProvision
    {
        [JsonPropertyName("BundleIdentifierPattern")]
        public string? BundleIdentifierPattern { get; set; }

        [JsonPropertyName("PrivateKeyPasswordlessP12Path"), JsonRequired]
        public string? PrivateKeyPasswordlessP12Path { get; set; }

        [JsonPropertyName("MobileProvisionPath"), JsonRequired]
        public string? MobileProvisionPath { get; set; }

        [JsonPropertyName("AppleProvidedCertificatePath"), JsonRequired]
        public string? AppleProvidedCertificatePath { get; set; }

        [JsonPropertyName("KeychainPasswordEnvironmentVariable")]
        public string? KeychainPasswordEnvironmentVariable { get; set; }
    }
}
