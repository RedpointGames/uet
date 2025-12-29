namespace Redpoint.Tpm.Internal
{
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    public interface ITpmService
    {
        Task<(byte[] ekPublicBytes, byte[] aikPublicBytes, ITpmOperationHandles operationHandles)> CreateRequestAsync();

        (string pem, string hash) GetPemAndHash(byte[] publicKeyBytes);

        RSAParameters GetRsaParameters(byte[] publicKeyBytes);

        (byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData) Authorize(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] data);

        byte[] DecryptSecretKey(ITpmOperationHandles operationHandles, byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData);
    }
}
