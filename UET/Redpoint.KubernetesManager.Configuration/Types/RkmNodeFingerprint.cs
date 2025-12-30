namespace Redpoint.KubernetesManager.Configuration.Types
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Reflection.Metadata;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;

    public static class RkmNodeFingerprint
    {
        public static string CreateFromPem(string pem)
        {
            var publicKey = new RSACryptoServiceProvider();
            publicKey.ImportFromPem(pem);

            var parameters = publicKey.ExportParameters(false);

            return Convert.ToHexStringLower(SHA256.HashData([
                .. parameters.Exponent ?? [],
                .. parameters.Modulus ?? []]));
        }

        public static string CreateFromClientCertificate(X509Certificate2 certificate, out string pem)
        {
            ArgumentNullException.ThrowIfNull(certificate);

            var commonName = certificate.GetNameInfo(X509NameType.SimpleName, false);
            var commonNameComponents = commonName.Split(':');
            if (commonNameComponents.Length != 3 || !string.Equals(commonNameComponents[0], "rkm.attestation", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Provided certificate does not contain expected common name format.");
            }

            var publicKey = new RSACryptoServiceProvider();
            var parameters = new RSAParameters
            {
                Exponent = Convert.FromBase64String(commonNameComponents[1]),
                Modulus = Convert.FromBase64String(commonNameComponents[2]),
            };
            publicKey.ImportParameters(parameters);

            pem = publicKey.ExportRSAPublicKeyPem();

            return Convert.ToHexStringLower(SHA256.HashData([
                .. parameters.Exponent ?? [],
                .. parameters.Modulus ?? []]));
        }
    }
}
