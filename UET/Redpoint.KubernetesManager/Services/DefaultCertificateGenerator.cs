namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using Redpoint.KubernetesManager.Services.Wsl;
    using System;
    using System.Globalization;
    using System.Net;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.RegularExpressions;

    internal class DefaultCertificateGenerator : ICertificateGenerator
    {
        private readonly IWslTranslation _wslTranslation;

        public DefaultCertificateGenerator(
            IWslTranslation wslTranslation)
        {
            _wslTranslation = wslTranslation;
        }

        public async Task<ExportedCertificate> GenerateCertificateAuthorityAsync(CancellationToken cancellationToken)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddIpAddress(IPAddress.Loopback);
            sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
            sanBuilder.AddIpAddress(await _wslTranslation.GetTranslatedIPAddress(cancellationToken));
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddDnsName(_wslTranslation.GetTranslatedControllerHostname());

            var dn = new StringBuilder();
            dn.Append(CultureInfo.InvariantCulture, $"CN=\"{_wslTranslation.GetTranslatedControllerHostname()}\"");

            var distinguishedName = new X500DistinguishedName(dn.ToString());

            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    distinguishedName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                var usages = X509KeyUsageFlags.KeyCertSign |
                    X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DataEncipherment |
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.NonRepudiation;
                request.CertificateExtensions.Add(new X509KeyUsageExtension(usages, false));
                request.CertificateExtensions.Add(sanBuilder.Build());
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));

                var cert = request.CreateSelfSigned(
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(-1).AddYears(10));

                return new ExportedCertificate(cert);
            }
        }

        public ExportedCertificate GenerateCertificate(ExportedCertificate certificateAuthority, string name, string role, string[]? additionalSubjectNames)
        {
            var sanBuilder = new SubjectAlternativeNameBuilder();
            if (additionalSubjectNames != null)
            {
                foreach (var sn in additionalSubjectNames)
                {
                    if (Regex.IsMatch(sn, "^[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+$"))
                    {
                        sanBuilder.AddIpAddress(IPAddress.Parse(sn));
                    }
                    else
                    {
                        sanBuilder.AddDnsName(sn);
                    }
                }
            }

            var dn = new StringBuilder();
            dn.Append(CultureInfo.InvariantCulture, $"CN=\"{name}\"");
            dn.Append(CultureInfo.InvariantCulture, $",O=\"{role}\"");

            var distinguishedName = new X500DistinguishedName(dn.ToString());

            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest(
                    distinguishedName,
                    rsa,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pkcs1);
                var usages = X509KeyUsageFlags.KeyCertSign |
                    X509KeyUsageFlags.KeyEncipherment |
                    X509KeyUsageFlags.DataEncipherment |
                    X509KeyUsageFlags.DigitalSignature |
                    X509KeyUsageFlags.NonRepudiation;
                request.CertificateExtensions.Add(new X509KeyUsageExtension(usages, false));
                request.CertificateExtensions.Add(sanBuilder.Build());
                request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, true, 1, true));

                var issuerCert = certificateAuthority.ToCertificate();
                var issuedCertWithoutPrivateKey = request.Create(
                    issuerCert,
                    issuerCert.NotBefore,
                    issuerCert.NotAfter,
                    BitConverter.GetBytes(DateTime.UtcNow.Ticks));
                var issuedCert = issuedCertWithoutPrivateKey.CopyWithPrivateKey(rsa);

                return new ExportedCertificate(issuedCert);
            }
        }
    }
}
