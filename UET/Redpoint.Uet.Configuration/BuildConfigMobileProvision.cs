namespace Redpoint.Uet.Configuration.Engine
{
    using System.Text.Json.Serialization;

    public class BuildConfigMobileProvision
    {
        [JsonPropertyName("BundleIdentifierPattern")]
        public string? BundleIdentifierPattern { get; set; }

        [JsonPropertyName("PublicKeyPemPath"), JsonRequired]
        public string? PublicKeyPemPath { get; set; }

        [JsonPropertyName("PrivateKeyPasswordlessP12Path"), JsonRequired]
        public string? PrivateKeyPasswordlessP12Path { get; set; }

        [JsonPropertyName("CertificateSigningRequestPath"), JsonRequired]
        public string? CertificateSigningRequestPath { get; set; }

        [JsonPropertyName("MobileProvisionPath"), JsonRequired]
        public string? MobileProvisionPath { get; set; }

        [JsonPropertyName("AppleProvidedCertificatePath"), JsonRequired]
        public string? AppleProvidedCertificatePath { get; set; }

        [JsonPropertyName("KeychainPasswordEnvironmentVariable"), JsonRequired]
        public string? KeychainPasswordEnvironmentVariable { get; set; }
    }
}
