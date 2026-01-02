namespace Redpoint.Tpm
{
    using NSec.Cryptography;
    using System.Security.Cryptography;
    using Tpm2Lib;

    internal static class SecurityConstants
    {
        public static readonly HashAlgorithmName RsaHashAlgorithmName = HashAlgorithmName.SHA256;

        public static readonly RSASignaturePadding RsaSignaturePadding = RSASignaturePadding.Pkcs1;

        public const int RsaKeyBitsCert = 3072;

        /// <remarks>
        /// The Hyper-V TPM does not support 3072-bit RSA, and this is likely the case for other TPMs as well. Therefore,
        /// use a different key size for TPMs.
        /// </remarks>
        public const int RsaKeyBitsTpm = 2048;

        public static readonly AeadAlgorithm SymmetricAlgorithm = AeadAlgorithm.XChaCha20Poly1305;

        public const KeyBlobFormat SymmetricKeyBlobFormat = KeyBlobFormat.NSecSymmetricKey;

        public const TpmAlgId TpmHashAlgorithmId = TpmAlgId.Sha256;
    }
}
