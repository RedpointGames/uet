namespace Redpoint.Tpm.Internal
{
    using Microsoft.Extensions.Logging;
    using NSec.Cryptography;
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

        public async Task<(byte[] ekPublicBytes, byte[] aikPublicBytes, ITpmOperationHandles operationHandles)> CreateRequestAsync()
        {
            var handles = new DefaultTpmOperationHandles();
            var returningHandles = false;
            try
            {
                handles._tpmDevice = OperatingSystem.IsWindows() ? new TbsDevice() : new LinuxTpmDevice();
                handles.TpmDevice.Connect();

                handles._tpm = new Tpm2(handles.TpmDevice);

                // Get EK.
                _logger.LogTrace("Getting EK handle...");
                handles._ekHandle = new TpmHandle(0x81010001);
                handles._ekPublic = handles.Tpm._AllowErrors().ReadPublic(
                    handles.EkHandle, out var ekName, out var ekQName);
                if (!handles.Tpm._LastCommandSucceeded())
                {
                    throw new InvalidOperationException("Failed to read public EK from TPM!");
                }

                // Create AIK.
                _logger.LogTrace("Creating AIK...");
                var aikPublicTemplate = new TpmPublic(
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

                var aikCreateResponse = await handles.Tpm.CreatePrimaryAsync(
                    TpmRh.Endorsement,
                    new SensitiveCreate(handles.Tpm.GetRandom(TpmHash.DigestSize(SecurityConstants.TpmHashAlgorithmId)), null),
                    aikPublicTemplate,
                    null,
                    null);

                handles._aikPublic = aikCreateResponse.outPublic;
                handles._aikHandle = aikCreateResponse.handle;

                returningHandles = true;
                return (
                    handles.EkPublic.GetTpmRepresentation(),
                    handles.AikPublic.GetTpmRepresentation(),
                    handles);
            }
            finally
            {
                if (!returningHandles)
                {
                    handles.Dispose();
                }
            }
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
            // Use libsodium to encrypt the data with a symmetric key, then
            // encrypt the symmetric key for decryption by the TPM.
            byte[] symmetricUnencryptedKey;
            byte[] symmetricEncryptedData;
            {
                var symmetricAlgorithm = SecurityConstants.SymmetricAlgorithm;
                var symmetricKey = Key.Create(symmetricAlgorithm, new KeyCreationParameters
                {
                    ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
                });
                var symmetricNonce = RandomNumberGenerator.GetBytes(symmetricAlgorithm.NonceSize);
                var symmetricEncryptedDataWithoutNonce = symmetricAlgorithm.Encrypt(
                    symmetricKey,
                    symmetricNonce,
                    [],
                    data);
                symmetricUnencryptedKey = symmetricKey.Export(SecurityConstants.SymmetricKeyBlobFormat);
                symmetricEncryptedData = [.. symmetricNonce, .. symmetricEncryptedDataWithoutNonce];
            }

            var ekPublicKey = new Marshaller(ekPublicBytes).Get<TpmPublic>();
            var aikPublicKey = new Marshaller(aikPublicBytes).Get<TpmPublic>();

            _logger.LogTrace("Creating activation credentials for AIK...");
            var certInfo = ekPublicKey.CreateActivationCredentials(
                symmetricUnencryptedKey,
                aikPublicKey.GetName(),
                out var aesEncryptedKey);

            var envelopingKeyMarshaller = new Marshaller();
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC.Length, "integrityHMAC.Length");
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC, "integrityHMAC");
            envelopingKeyMarshaller.Put(certInfo.encIdentity.Length, "encIdentity");
            envelopingKeyMarshaller.Put(certInfo.encIdentity, "encIdentity.Length");
            byte[] envelopingKey = envelopingKeyMarshaller.GetBytes();

            return (envelopingKey, aesEncryptedKey, symmetricEncryptedData);
        }

        public byte[] DecryptSecretKey(ITpmOperationHandles handles, byte[] envelopingKeyBytes, byte[] encryptedKey, byte[] encryptedData)
        {
            try
            {
                // Get enveloping key.
                IdObject envelopingKey = new IdObject();
                {
                    var envelopingKeyMarshaller = new Marshaller(envelopingKeyBytes);
                    int len = envelopingKeyMarshaller.Get<int>();
                    envelopingKey.integrityHMAC = envelopingKeyMarshaller.GetArray<byte>(len);
                    len = envelopingKeyMarshaller.Get<int>();
                    envelopingKey.encIdentity = envelopingKeyMarshaller.GetArray<byte>(len);
                }

                // Create sessions for AIK usage.
                _logger.LogTrace("Activating AIK...");
                var aikSession = handles.Tpm.StartAuthSessionEx(
                    TpmSe.Policy,
                    SecurityConstants.TpmHashAlgorithmId);
                using var aikSessionAutoFlush = new TpmAutoFlush(handles.Tpm, aikSession);
                aikSession.RunPolicy(handles.Tpm, GetAikPolicyTree(), "Activate");

                // Determine policy tree for EK usage.
                var ekPolicyTree = new PolicyTree(handles.EkPublic.nameAlg);
                ekPolicyTree.SetPolicyRoot(new TpmPolicySecret(
                    TpmRh.Endorsement,
                    false,
                    0,
                    null,
                    null));

                // Create session for EK usage.
                var ekSession = handles.Tpm.StartAuthSessionEx(
                    TpmSe.Policy,
                    handles.EkPublic.nameAlg);
                using var ekSessionAutoFlush = new TpmAutoFlush(handles.Tpm, ekSession);
                ekSession.RunPolicy(handles.Tpm, ekPolicyTree);

                // Activate the AIK credential.
                var symmetricUnencryptedKey = handles.Tpm[aikSession, ekSession]
                    .ActivateCredential(
                        handles.AikHandle,
                        handles.EkHandle,
                        envelopingKey,
                        encryptedKey);

                // Use libsodium to decrypt the data with the decrypted symmetric key.
                {
                    var symmetricAlgorithm = SecurityConstants.SymmetricAlgorithm;
                    var symmetricKey = Key.Import(
                        symmetricAlgorithm,
                        symmetricUnencryptedKey,
                        SecurityConstants.SymmetricKeyBlobFormat);
                    var symmetricNonce = encryptedData[0..symmetricAlgorithm.NonceSize];
                    var symmetricEncryptedDataWithoutNonce = encryptedData[symmetricAlgorithm.NonceSize..];
                    return symmetricAlgorithm.Decrypt(
                        symmetricKey,
                        symmetricNonce,
                        [],
                        symmetricEncryptedDataWithoutNonce)!;
                }
            }
            finally
            {
                handles.Dispose();
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
