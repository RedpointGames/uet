namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Models;
    using System.Text;
    using System.Text.RegularExpressions;

    internal class DefaultKubeConfigGenerator : IKubeConfigGenerator
    {
        public string GenerateKubeConfig(
            string certificateAuthorityPem,
            string primaryNodeAddress,
            ExportedCertificate userCertificate)
        {
            var username = Regex.Match(
                userCertificate.ToCertificate().SubjectName.Name,
                "CN=([a-z0-9:-_]+)").Groups[1].Value;
            return @$"
apiVersion: v1
kind: Config
clusters:
  - name: kubernetes
    cluster:
      server: https://{primaryNodeAddress}:6443
      certificate-authority-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(certificateAuthorityPem))}
users:
  - name: ""{username}""
    user:
      client-certificate-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(userCertificate.CertificatePem))}
      client-key-data: {Convert.ToBase64String(Encoding.UTF8.GetBytes(userCertificate.PrivateKeyPem))}
contexts:
  - name: default
    context:
      cluster: kubernetes
      name: default
      user: ""{username}""
current-context: default
".Trim() + "\n";
        }
    }
}
