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
    }
}
