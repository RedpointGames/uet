namespace Redpoint.Tpm.Internal
{
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    internal interface ITpmService
    {
        Task<(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] aikContextBytes)> CreateRequestAsync();

        (string pem, string hash) GetPemAndHash(byte[] publicKeyBytes);

        RSAParameters GetRsaParameters(byte[] publicKeyBytes);

        (byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData) Authorize(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] data);

        byte[] DecryptSecretKey(byte[] aikContextBytes, byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData);
    }
}
