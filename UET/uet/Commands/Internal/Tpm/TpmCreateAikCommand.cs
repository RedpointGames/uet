namespace UET.Commands.Internal.Tpm
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Threading.Tasks;
    using Tpm2Lib;

    internal sealed class TpmCreateAikCommand
    {
        internal sealed class Options
        {
        }

        public static Command CreateTpmCreateAikCommand()
        {
            var options = new Options();
            var command = new Command("create-aik");
            command.AddAllOptions(options);
            command.AddCommonHandler<TpmCreateAikCommandInstance>(options);
            return command;
        }

        private sealed class TpmCreateAikCommandInstance : ICommandInstance
        {
            private readonly ILogger<TpmCreateAikCommandInstance> _logger;

            public TpmCreateAikCommandInstance(
                ILogger<TpmCreateAikCommandInstance> logger)
            {
                _logger = logger;
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

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                using var tpmDevice = new TbsDevice();
                tpmDevice.Connect();

                using var tpm = new Tpm2(tpmDevice);

                // Set algorithm.
                var alg = TpmAlgId.Sha256;

                // Get EK.
                _logger.LogInformation("Getting EK handle...");
                var ekHandle = new TpmHandle(0x81010001);
                var ekPublicKey = tpm._AllowErrors().ReadPublic(ekHandle, out var ekName, out var ekQName);
                if (!tpm._LastCommandSucceeded())
                {
                    _logger.LogError("Failed to read public EK from TPM!");
                    return 1;
                }

                // Create AIK.
                _logger.LogInformation("Creating AIK...");
                var aikPolicyTree = new PolicyTree(alg);
                var aikPolicyOr = new TpmPolicyOr();
                aikPolicyTree.SetPolicyRoot(aikPolicyOr);
                aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.ActivateCredential, "Activate"));
                aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.Certify, "Certify"));
                aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.CertifyCreation, "CertifyCreation"));
                aikPolicyOr.AddPolicyBranch(new TpmPolicyCommand(TpmCc.Quote, "Quote"));

                var aikPublicKey = new TpmPublic(
                    alg,
                    ObjectAttr.Restricted |
                        ObjectAttr.Sign |
                        ObjectAttr.FixedParent |
                        ObjectAttr.FixedTPM |
                        ObjectAttr.AdminWithPolicy |
                        ObjectAttr.SensitiveDataOrigin,
                    aikPolicyTree.GetPolicyDigest(),
                    new RsaParms(new SymDefObject(), new SchemeRsassa(alg), 2048, 0),
                    new Tpm2bPublicKeyRsa());

                var aikCreateResponse = await tpm.CreatePrimaryAsync(
                    TpmRh.Endorsement,
                    new SensitiveCreate(tpm.GetRandom(TpmHash.DigestSize(alg)), null),
                    aikPublicKey,
                    null,
                    null);

                aikPublicKey = aikCreateResponse.outPublic;
                var aikHandle = aikCreateResponse.handle;
                using var aikHandleAutoFlush = new TpmAutoFlush(tpm, aikHandle);

                // Create activation credentials.
                _logger.LogInformation("Creating activation credentials for AIK...");
                var certInfo = ekPublicKey.CreateActivationCredentials(
                    tpm.GetRandom(32),
                    aikPublicKey.GetName(),
                    out var encryptedSecret);

                // Create sessions for AIK usage.
                _logger.LogInformation("Activating AIK...");
                var aikSession = tpm.StartAuthSessionEx(
                    TpmSe.Policy,
                    alg);
                using var aikSessionAutoFlush = new TpmAutoFlush(tpm, aikSession);
                aikSession.RunPolicy(tpm, aikPolicyTree, "Activate");

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
                var recoveredSecretKey = tpm[aikSession, ekSession]
                    .ActivateCredential(
                        aikHandle,
                        ekHandle,
                        certInfo,
                        encryptedSecret);

                // Export PEM.
                _logger.LogInformation($"EK: {ToPem(ekPublicKey)}");
                _logger.LogInformation($"AIK: {ToPem(aikPublicKey)}");

                _logger.LogInformation($"EK Hash: {ToHash(ekPublicKey)}");
                _logger.LogInformation($"AIK Hash: {ToHash(aikPublicKey)}");

                return 0;
            }

            private static string ToPem(TpmPublic key)
            {
                var rsaParams = (RsaParms)key.parameters;
                var exponent = rsaParams.exponent != 0 ? Globs.HostToNet(rsaParams.exponent) : RsaParms.DefaultExponent;
                var modulus = (key.unique as Tpm2bPublicKeyRsa)!.buffer;

                var publicKey = new RSACryptoServiceProvider();
                publicKey.ImportParameters(new RSAParameters
                {
                    Exponent = exponent,
                    Modulus = modulus,
                });
                return publicKey.ExportRSAPublicKeyPem();
            }

            private static string ToHash(TpmPublic key)
            {
                var rsaParams = (RsaParms)key.parameters;
                var exponent = rsaParams.exponent != 0 ? Globs.HostToNet(rsaParams.exponent) : RsaParms.DefaultExponent;
                var modulus = (key.unique as Tpm2bPublicKeyRsa)!.buffer;

                return Convert.ToHexStringLower(SHA256.HashData(exponent.Concat(modulus).ToArray()));
            }
        }
    }
}
