namespace Redpoint.Tpm
{
    using NSec.Cryptography;
    using System.Security.Cryptography;
    using Tpm2Lib;

    internal static class SecurityConstants
    {
        public static readonly HashAlgorithmName RsaHashAlgorithmName = HashAlgorithmName.SHA256;

        public static readonly RSASignaturePadding RsaSignaturePadding = RSASignaturePadding.Pkcs1;

        public const int RsaKeyBits = 3072;

        public static readonly AeadAlgorithm SymmetricAlgorithm = AeadAlgorithm.XChaCha20Poly1305;

        public const KeyBlobFormat SymmetricKeyBlobFormat = KeyBlobFormat.NSecSymmetricKey;

        public const TpmAlgId TpmHashAlgorithmId = TpmAlgId.Sha256;
    }
}
