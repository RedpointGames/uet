namespace Redpoint.KubernetesManager.Tpm
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;

    public interface ITpmCertificateService
    {
        (CertificateRequest csr, RSA privateKey) CreatePrivateKeyAndCsrForAik(byte[] aikPublicBytes);

        X509Certificate2 SignCsrWithCertificateAuthority(CertificateRequest csr, X509Certificate2 certificateAuthority);
    }
}
