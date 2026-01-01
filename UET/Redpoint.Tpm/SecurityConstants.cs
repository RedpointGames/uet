namespace Redpoint.Tpm
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading.Tasks;
    using Tpm2Lib;

    internal static class SecurityConstants
    {
        public static readonly HashAlgorithmName RsaHashAlgorithmName = HashAlgorithmName.SHA256;

        public static readonly RSASignaturePadding RsaSignaturePadding = RSASignaturePadding.Pkcs1;

        public const int RsaKeyBits = 3072;

        public const int AesKeySize = 256;

        public const int AesBlockSize = 128;

        public const int AesIvSize = AesBlockSize / 8;

        public const TpmAlgId TpmHashAlgorithmId = TpmAlgId.Sha256;
    }
}
