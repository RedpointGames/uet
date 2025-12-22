namespace Redpoint.KubernetesManager.Services
{
    using Redpoint.KubernetesManager.Manifest;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using YamlDotNet.Core.Tokens;

    internal interface ITpmService
    {
        Task<(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] aikContextBytes)> CreateRequestAsync();

        (string pem, string hash) GetPemAndHash(byte[] publicKeyBytes);

        (byte[] envelopingKey, byte[] encryptedData) Authorize(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] data);

        byte[] DecryptSecretKey(byte[] aikContextBytes, byte[] envelopingKey, byte[] encryptedSecret);
    }
}
