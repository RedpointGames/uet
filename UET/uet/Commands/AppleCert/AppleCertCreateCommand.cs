namespace UET.Commands.AppleCert
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal sealed class AppleCertCreateCommand
    {
        internal sealed class Options
        {
            public Option<string> Name;
            public Option<string> AppleEmailAddress;
            public Option<DirectoryInfo> StoragePath;

            public Options()
            {
                Name = new Option<string>("--name")
                {
                    Description = "The name of this certificate. Should be an alphanumeric string with no spaces, and must be the same when running `uet apple-cert finalize`.",
                    IsRequired = true,
                };
                Name.AddAlias("-n");

                AppleEmailAddress = new Option<string>("--apple-email-address")
                {
                    Description = "The email address of your Apple developer account.",
                    IsRequired = true,
                };
                AppleEmailAddress.AddAlias("-e");

                StoragePath = new Option<DirectoryInfo>("--storage-path")
                {
                    Description = "The path to store certificates. This must be the same between both commands and is recommended to be the Build/IOS/ folder underneath your project. If not set, defaults to the current directory.",
                };
                StoragePath.AddAlias("-s");
            }
        }

        public static Command CreateAppleCertCreateCommand()
        {
            var options = new Options();
            var command = new Command("create", "(step 1) Create a private key and signing request to submit to the Apple Developer portal.");
            command.AddAllOptions(options);
            command.AddCommonHandler<CreateAppleCertCreateCommandInstance>(options);
            return command;
        }

        private sealed class CreateAppleCertCreateCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateAppleCertCreateCommandInstance> _logger;
            private readonly Options _options;

            public CreateAppleCertCreateCommandInstance(
                ILogger<CreateAppleCertCreateCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            private static readonly Regex _nameRegex = new Regex("^[a-zA-Z]+[a-zA-Z0-9]*$");

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForOption(_options.Name);
                var appleEmailAddress = context.ParseResult.GetValueForOption(_options.AppleEmailAddress);
                var storagePath = context.ParseResult.GetValueForOption(_options.StoragePath);

                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogError("--name must be set.");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(appleEmailAddress))
                {
                    _logger.LogError("--apple-email-address must be set.");
                    return 1;
                }
                if (!_nameRegex.IsMatch(name))
                {
                    _logger.LogError("--name must match the regex '^[a-zA-Z]+[a-zA-Z0-9]*$' (an alphanumeric string).");
                    return 1;
                }

                // Default the storage path to the current directory, otherwise ensure the storage path exists.
                if (storagePath == null)
                {
                    storagePath = new DirectoryInfo(Environment.CurrentDirectory);
                }
                else
                {
                    storagePath.Create();
                }

                // Generate the private key and save it so we can combine it with the provided certificate in `uet apple-cert finalize` later.
                using var privateKey = RSA.Create(2048);
                using (var file = new FileStream(Path.Combine(storagePath.FullName, $"{name}.key"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var writer = new StreamWriter(file, leaveOpen: true))
                    {
                        await writer.WriteAsync(privateKey.ExportRSAPrivateKeyPem()).ConfigureAwait(false);
                    }
                }

                // Generate the certificate signing request for the user to submit to Apple.
                var distinguishedName = new X500DistinguishedName($"1.2.840.113549.1.9.1={appleEmailAddress}");
                using (var file = new FileStream(Path.Combine(storagePath.FullName, $"{name}.csr"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    using (var writer = new StreamWriter(file, leaveOpen: true))
                    {
                        var request = new CertificateRequest(distinguishedName, privateKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                        await writer.WriteAsync(request.CreateSigningRequestPem()).ConfigureAwait(false);
                    }
                }

                _logger.LogInformation("Private key and certificate signing request successfully created. Your next steps are:");
                _logger.LogInformation("");
                _logger.LogInformation("1. Go to https://developer.apple.com/account/resources/certificates/list and click '+'.");
                _logger.LogInformation("2. Select the software type, then click 'Continue'.");
                _logger.LogInformation("   - Pick 'Apple Development' if you want to use this certificate for debugging and testing your game.");
                _logger.LogInformation("   - Pick 'Apple Distribution' if you want to use this certificate for making a packaged version of your game that you submit for App Store review.");
                _logger.LogInformation("   - If these options are grayed out, then you already have existing certificates for this purpose. You will need to revoke the existing certificates before you can generate new ones.");
                _logger.LogInformation("   - You can also select 'iOS App Development' or 'iOS Distribution' if you're only targeting iOS. These certificate types have a higher limit than 'Apple Development' and 'Apple Distribution'.");
                _logger.LogInformation($"3. Select the certificate signing request that UET just created from: {Path.Combine(storagePath.FullName, $"{name}.csr")}");
                _logger.LogInformation($"4. Click 'Continue'.");
                _logger.LogInformation($"5. Click 'Download'.");
                _logger.LogInformation($"6. After the file is downloaded, it will likely be called 'development.cer'. Move it to '{storagePath.FullName}' and rename the file to '{name}.cer'.");
                _logger.LogInformation($"7. Run `uet apple-cert finalize --name \"{name}\" --storage-path \"{storagePath.FullName}\"`.");
                return 0;
            }
        }
    }
}
