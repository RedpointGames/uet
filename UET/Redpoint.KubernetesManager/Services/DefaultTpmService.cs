namespace Redpoint.KubernetesManager.Services
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

        private const TpmAlgId _tpmAlgId = TpmAlgId.Sha256;

        private static PolicyTree GetAikPolicyTree()
        {
            var aikPolicyTree = new PolicyTree(_tpmAlgId);
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
                _tpmAlgId,
                ObjectAttr.Restricted |
                    ObjectAttr.Sign |
                    ObjectAttr.FixedParent |
                    ObjectAttr.FixedTPM |
                    ObjectAttr.AdminWithPolicy |
                    ObjectAttr.SensitiveDataOrigin,
                GetAikPolicyTree().GetPolicyDigest(),
                new RsaParms(new SymDefObject(), new SchemeRsassa(_tpmAlgId), 2048, 0),
                new Tpm2bPublicKeyRsa());

            var aikCreateResponse = await tpm.CreatePrimaryAsync(
                TpmRh.Endorsement,
                new SensitiveCreate(tpm.GetRandom(TpmHash.DigestSize(_tpmAlgId)), null),
                aikPublicKey,
                null,
                null);

            aikPublicKey = aikCreateResponse.outPublic;
            var aikContext = tpm.ContextSave(aikCreateResponse.handle);

            return (
                ekPublicKey.GetTpmRepresentation(),
                aikPublicKey.GetTpmRepresentation(),
                aikContext.GetTpmRepresentation());
        }

        public (string pem, string hash) GetPemAndHash(byte[] publicKeyBytes)
        {
            var tpmPublicKey = new Marshaller(publicKeyBytes).Get<TpmPublic>();

            var rsaParams = (RsaParms)tpmPublicKey.parameters;
            var exponent = rsaParams.exponent != 0 ? Globs.HostToNet(rsaParams.exponent) : RsaParms.DefaultExponent;
            var modulus = (tpmPublicKey.unique as Tpm2bPublicKeyRsa)!.buffer;

            var publicKey = new RSACryptoServiceProvider();
            publicKey.ImportParameters(new RSAParameters
            {
                Exponent = exponent,
                Modulus = modulus,
            });

            return (publicKey.ExportRSAPublicKeyPem(), Convert.ToHexStringLower(SHA256.HashData([.. exponent, .. modulus])));
        }

        public (byte[] envelopingKey, byte[] encryptedData) Authorize(byte[] ekPublicBytes, byte[] aikPublicBytes, byte[] data)
        {
            using Tpm2Device tpmDevice = OperatingSystem.IsWindows() ? new TbsDevice() : new LinuxTpmDevice();
            tpmDevice.Connect();

            using var tpm = new Tpm2(tpmDevice);

            var ekPublicKey = new Marshaller(ekPublicBytes).Get<TpmPublic>();
            var aikPublicKey = new Marshaller(aikPublicBytes).Get<TpmPublic>();

            _logger.LogInformation("Creating activation credentials for AIK...");
            var certInfo = ekPublicKey.CreateActivationCredentials(
                data,
                aikPublicKey.GetName(),
                out var encryptedData);

            var envelopingKeyMarshaller = new Marshaller();
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC.Length, "integrityHMAC.Length");
            envelopingKeyMarshaller.Put(certInfo.integrityHMAC, "integrityHMAC");
            envelopingKeyMarshaller.Put(certInfo.encIdentity.Length, "encIdentity");
            envelopingKeyMarshaller.Put(certInfo.encIdentity, "encIdentity.Length");
            byte[] envelopingKey = envelopingKeyMarshaller.GetBytes();

            return (envelopingKey, encryptedData);
        }

        public byte[] DecryptSecretKey(byte[] aikContextBytes, byte[] envelopingKeyBytes, byte[] encryptedSecret)
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
            var aikContext = new Marshaller(aikContextBytes).Get<Context>();
            var aikHandle = tpm.ContextLoad(aikContext);

            // Create sessions for AIK usage.
            _logger.LogInformation("Activating AIK...");
            var aikSession = tpm.StartAuthSessionEx(
                TpmSe.Policy,
                _tpmAlgId);
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
            return tpm[aikSession, ekSession]
                .ActivateCredential(
                    aikHandle,
                    ekHandle,
                    envelopingKey,
                    encryptedSecret);
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
