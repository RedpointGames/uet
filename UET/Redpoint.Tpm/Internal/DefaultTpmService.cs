namespace Redpoint.Tpm.Internal
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Tpm2Lib;

    internal class DefaultTpmService : ITpmService
    {
        private readonly ILogger<DefaultTpmService> _logger;

        public DefaultTpmService(
            ILogger<DefaultTpmService> logger)
        {
            _logger = logger;
        }

        private static PolicyTree GetAikPolicyTree()
        {
            var aikPolicyTree = new PolicyTree(SecurityConstants.TpmHashAlgorithmId);
            var aikPolicyOr = new TpmPolicyOr();
            aikPolicyTree.SetPolicyRoot(aikPolicyOr);
            aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.ActivateCredential, "Activate"));
            aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.Certify, "Certify"));
            aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.CertifyCreation, "CertifyCreation"));
            aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.Quote, "Quote"));
            return aikPolicyTree;
        }

        public async Task<(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] aikContextBytes)> CreateRequestAsync()
        {
            using Tpm2Device tpmDevice = OperatingSystem.IsWindows() ? new TbsDevice() : new LinuxTpmDevice();
            tpmDevice.Connect();

            using var tpm = new Tpm2(tpmDevice);

            // Get EK.
            _logger.LogInformation("Getting EK handle...");
            var ekHandle = new TpmHandle(0x81010001);
            var ekPublicKey = tpm._AllowErrors().ReadPublic(ekHandle, out var ekName, out var ekQName);
            if (!tpm._LastCommandSucceeded())
            {
                throw new InvalidOperationException("Failed to read public EK from TPM!");
            }

            // Create AIK.
            _logger.LogInformation("Creating AIK...");
            var aikPublicKey = new TpmPublic(
                SecurityConstants.TpmHashAlgorithmId,
                ObjectAttr.Restricted |
                    ObjectAttr.Sign |
                    ObjectAttr.FixedParent |
                    ObjectAttr.FixedTPM |
                    ObjectAttr.AdminWithPolicy |
                    ObjectAttr.SensitiveDataOrigin,
                GetAikPolicyTree().GetPolicyDigest(),
                new RsaParms(new SymDefObject(), new SchemeRsassa(SecurityConstants.TpmHashAlgorithmId), SecurityConstants.RsaKeyBits, 0),
                new Tpm2bPublicKeyRsa());

            var aikCreateResponse = await tpm.CreatePrimaryAsync(
                TpmRh.Endorsement,
                new SensitiveCreate(tpm.GetRandom(TpmHash.DigestSize(SecurityConstants.TpmHashAlgorithmId)), null),
                aikPublicKey,
                null,
                null);

            aikPublicKey = aikCreateResponse.outPublic;
            var aikContext = tpm._AllowErrors().ContextSave(aikCreateResponse.handle);

            return (
                ekPublicKey.GetTpmRepresentation(),
                aikPublicKey.GetTpmRepresentation(),
                aikContext?.GetTpmRepresentation() ?? []);
        }

        public (string pem, string hash) GetPemAndHash(byte[] publicKeyBytes)
        {
            var parameters = GetRsaParameters(publicKeyBytes);

            var publicKey = new RSACryptoServiceProvider();
            publicKey.ImportParameters(parameters);

            return (publicKey.ExportRSAPublicKeyPem(), Convert.ToHexStringLower(SHA256.HashData([.. parameters.Exponent!, .. parameters.Modulus!])));
        }

        public RSAParameters GetRsaParameters(byte[] publicKeyBytes)
        {
            var tpmPublicKey = new Marshaller(publicKeyBytes).Get<TpmPublic>();

            var rsaParams = (RsaParms)tpmPublicKey.parameters;
            var exponent = rsaParams.exponent != 0 ? Globs.HostToNet(rsaParams.exponent) : RsaParms.DefaultExponent;
            var modulus = (tpmPublicKey.unique as Tpm2bPublicKeyRsa)!.buffer;

            return new RSAParameters
            {
                Exponent = exponent,
                Modulus = modulus,
            };
        }

        public (byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData) Authorize(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] data)
        {
            // Generate an AES key and encrypt the data with it. We'll then encrypt the AES
            // key with the TPM AIK. This is required because the TPM ActivateCredential() command 
            // doesn't work if the original data is longer than 256 bytes.
            byte[] aesUnencryptedKey;
            byte[] aesEncryptedData;
            {
                using var aes = Aes.Create();
                aes.KeySize = SecurityConstants.AesKeySize;
                aes.BlockSize = SecurityConstants.AesBlockSize;
                aes.GenerateKey();
                aes.GenerateIV();
                using var encryptor = aes.CreateEncryptor();
                using var memoryStream = new MemoryStream();
                using (var encryptStream = new CryptoStream(memoryStream, encryptor, CryptoStreamMode.Write))
                {
                    encryptStream.Write(data);
                }
                aesUnencryptedKey = aes.Key;
                aesEncryptedData = [.. aes.IV, .. memoryStream.ToArray()];
            }

            var ekPublicKey = new Marshaller(ekPublicBytes).Get<TpmPublic>();
            var aikPublicKey = new Marshaller(aikPublicBytes).Get<TpmPublic>();

            _logger.LogInformation("Creating activation credentials for AIK...");
            var certInfo = ekPublicKey.CreateActivationCredentials(
                aesUnencryptedKey,
                aikPublicKey.GetName(),
                out var aesEncryptedKey);

            var envelopingKeyMarshaller = new Marshaller();
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC.Length, "integrityHMAC.Length");
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC, "integrityHMAC");
            envelopingKeyMarshaller.Put(certInfo.encIdentity.Length, "encIdentity");
            envelopingKeyMarshaller.Put(certInfo.encIdentity, "encIdentity.Length");
            byte[] envelopingKey = envelopingKeyMarshaller.GetBytes();

            return (envelopingKey, aesEncryptedKey, aesEncryptedData);
        }

        public byte[] DecryptSecretKey(byte[] aikContextBytes, byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData)
        {
            using Tpm2Device tpmDevice = OperatingSystem.IsWindows() ? new TbsDevice() : new LinuxTpmDevice();
            tpmDevice.Connect();

            using var tpm = new Tpm2(tpmDevice);

            // Get enveloping key.
            IdObject envelopingKey = new IdObject();
            {
                var envelopingKeyMarshaller = new Marshaller(envelopingKeyBytes);
                int len = envelopingKeyMarshaller.Get<int>();
                envelopingKey.integrityHMAC = envelopingKeyMarshaller.GetArray<byte>(len);
                len = envelopingKeyMarshaller.Get<int>();
                envelopingKey.encIdentity = envelopingKeyMarshaller.GetArray<byte>(len);
            }

            // Get EK.
            _logger.LogInformation("Getting EK handle...");
            var ekHandle = new TpmHandle(0x81010001);
            var ekPublicKey = tpm._AllowErrors().ReadPublic(ekHandle, out var ekName, out var ekQName);
            if (!tpm._LastCommandSucceeded())
            {
                throw new InvalidOperationException("Failed to read public EK from TPM!");
            }

            // Load AIK context.
            TpmHandle? aikHandle;
            if (aikContextBytes.Length == 0)
            {
                // When running under Windows, we can't use ContextSave/ContextLoad for the AIK,
                // so we just resubmit the same request as earlier and hope that the AIK hasn't changed.
                var aikPublicKey = new TpmPublic(
                    SecurityConstants.TpmHashAlgorithmId,
                    ObjectAttr.Restricted |
                        ObjectAttr.Sign |
                        ObjectAttr.FixedParent |
                        ObjectAttr.FixedTPM |
                        ObjectAttr.AdminWithPolicy |
                        ObjectAttr.SensitiveDataOrigin,
                    GetAikPolicyTree().GetPolicyDigest(),
                    new RsaParms(new SymDefObject(), new SchemeRsassa(SecurityConstants.TpmHashAlgorithmId), SecurityConstants.RsaKeyBits, 0),
                    new Tpm2bPublicKeyRsa());
                aikHandle = tpm.CreatePrimary(
                    TpmRh.Endorsement,
                    new SensitiveCreate(tpm.GetRandom(TpmHash.DigestSize(SecurityConstants.TpmHashAlgorithmId)), null),
                    aikPublicKey,
                    null,
                    null,
                    out _,
                    out _,
                    out _,
                    out _);
            }
            else
            {
                var aikContext = new Marshaller(aikContextBytes).Get<Context>();
                aikHandle = tpm.ContextLoad(aikContext);
            }

            // Create sessions for AIK usage.
            _logger.LogInformation("Activating AIK...");
            var aikSession = tpm.StartAuthSessionEx(
                TpmSe.Policy,
                SecurityConstants.TpmHashAlgorithmId);
            using var aikSessionAutoFlush = new TpmAutoFlush(tpm, aikSession);
            aikSession.RunPolicy(tpm, GetAikPolicyTree(), "Activate");

            // Determine policy tree for EK usage.
            var ekPolicyTree = new PolicyTree(ekPublicKey.nameAlg);
            ekPolicyTree.SetPolicyRoot(new TpmPolicySecret(
                TpmRh.Endorsement,
                false,
                0,
                null,
                null));

            // Create session for EK usage.
            var ekSession = tpm.StartAuthSessionEx(
                TpmSe.Policy,
                ekPublicKey.nameAlg);
            using var ekSessionAutoFlush = new TpmAutoFlush(tpm, ekSession);
            ekSession.RunPolicy(tpm, ekPolicyTree);

            // Activate the AIK credential.
            var aesUnencryptedKey = tpm[aikSession, ekSession]
                .ActivateCredential(
                    aikHandle,
                    ekHandle,
                    envelopingKey,
                    encryptedKey);

            // Use AES to decrypt the encrypted data with the now decrypted key.
            {
                using var aes = Aes.Create();
                aes.Key = aesUnencryptedKey;
                aes.IV = encryptedData[0..SecurityConstants.AesIvSize];
                using var decryptor = aes.CreateDecryptor();
                using var memoryStream = new MemoryStream(encryptedData[SecurityConstants.AesIvSize..]);
                byte[] decryptedData;
                using (var encryptStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read))
                {
                    using var decryptedStream = new MemoryStream();
                    encryptStream.CopyTo(decryptedStream);
                    decryptedData = decryptedStream.ToArray();
                }
                return decryptedData;
            }
        }

        private class TpmAutoFlush : IDisposable
        {
            private readonly Tpm2 _tpm;
            private readonly TpmHandle _handle;

            public TpmAutoFlush(Tpm2 tpm, TpmHandle handle)
            {
                _tpm = tpm;
                _handle = handle;
            }

            public void Dispose()
            {
                _tpm.FlushContext(_handle);
            }
        }
    }
}
