namespace UET.Commands.AppleCert
{
    using Microsoft.Extensions.Logging;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal sealed class AppleCertFinalizeCommand
    {
        internal sealed class Options
        {
            public Option<string> Name;
            public Option<DirectoryInfo> StoragePath;

            public Options()
            {
                Name = new Option<string>("--name")
                {
                    Description = "The name of this certificate. Should be an alphanumeric string with no spaces, and must be the same as when you ran `uet apple-cert create`.",
                    IsRequired = true,
                };
                Name.AddAlias("-n");

                StoragePath = new Option<DirectoryInfo>("--storage-path")
                {
                    Description = "The path to store certificates. This must be the same between both commands and is recommended to be the Build/IOS/ folder underneath your project. If not set, defaults to the current directory.",
                };
                StoragePath.AddAlias("-s");
            }
        }

        public static Command CreateAppleCertFinalizeCommand()
        {
            var options = new Options();
            var command = new Command("finalize", "(step 2) Combine the downloaded certificate into a .p12 file you can import into the Unreal Engine Project Settings.");
            command.AddAllOptions(options);
            command.AddCommonHandler<CreateAppleCertFinalizeCommandInstance>(options);
            return command;
        }

        private sealed class CreateAppleCertFinalizeCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateAppleCertFinalizeCommandInstance> _logger;
            private readonly Options _options;

            public CreateAppleCertFinalizeCommandInstance(
                ILogger<CreateAppleCertFinalizeCommandInstance> logger,
                Options options)
            {
                _logger = logger;
                _options = options;
            }

            private static readonly Regex _nameRegex = new Regex("^[a-zA-Z]+[a-zA-Z0-9]*$");

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var name = context.ParseResult.GetValueForOption(_options.Name);
                var storagePath = context.ParseResult.GetValueForOption(_options.StoragePath);

                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogError("--name must be set.");
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

                // Ensure our expected files exist.
                if (!File.Exists(Path.Combine(storagePath.FullName, $"{name}.key")))
                {
                    _logger.LogError($"Please run `uet apple-cert create` first; '{Path.Combine(storagePath.FullName, $"{name}.key")}' does not exist.");
                    return 1;
                }
                if (!File.Exists(Path.Combine(storagePath.FullName, $"{name}.cer")))
                {
                    _logger.LogError($"Please submit the certificate signing request to Apple and download the certificate; '{Path.Combine(storagePath.FullName, $"{name}.cer")}' does not exist. For steps on how to obtain this file, run `uet apple-cert create`.");
                    return 1;
                }

                // Import the private key.
                using var privateKey = RSA.Create();
                using (var file = new FileStream(Path.Combine(storagePath.FullName, $"{name}.key"), FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (var reader = new StreamReader(file, leaveOpen: true))
                    {
                        privateKey.ImportFromPem(await reader.ReadToEndAsync().ConfigureAwait(false));
                    }
                }

                // Import the signed certificate from Apple.
                var publicCertificate = new X509Certificate2(Path.Combine(storagePath.FullName, $"{name}.cer"), (string?)null, X509KeyStorageFlags.Exportable);

                // Attach the private key to the public certificate.
                publicCertificate = publicCertificate.CopyWithPrivateKey(privateKey);
                if (OperatingSystem.IsWindows())
                {
                    publicCertificate.FriendlyName = name;
                }

                // Import the intermediate certificates provided by Apple, bundled with UET.
                var intermediateCertificates = new List<X509Certificate2>();
                foreach (var embeddedResourceName in Assembly.GetExecutingAssembly().GetManifestResourceNames())
                {
                    if (embeddedResourceName.StartsWith("UET.Commands.AppleCert.", StringComparison.OrdinalIgnoreCase) &&
                        embeddedResourceName.EndsWith(".cer", StringComparison.OrdinalIgnoreCase))
                    {
                        using (var embeddedResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(embeddedResourceName)!)
                        {
                            var bytes = new byte[embeddedResourceStream.Length];
                            embeddedResourceStream.ReadExactly(bytes);
                            intermediateCertificates.Add(new X509Certificate2(bytes, (string?)null));
                        }
                    }
                }

                // Combine the certificates into a collection.
                var collection = new X509Certificate2Collection();
                foreach (var intermediateCertificate in intermediateCertificates)
                {
                    collection.Add(intermediateCertificate);
                }
                collection.Add(publicCertificate);

                // Export as a .p12 file that can be imported.
                using (var file = new FileStream(Path.Combine(storagePath.FullName, $"{name}.p12"), FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                {
                    await file.WriteAsync(collection.Export(X509ContentType.Pkcs12, null)).ConfigureAwait(false);
                    await file.FlushAsync().ConfigureAwait(false);
                }

                // We're done.
                _logger.LogInformation($"Certificate bundle successfully exported to '{Path.Combine(storagePath.FullName, $"{name}.p12")}'.");
                _logger.LogInformation("- You can now import the .p12 file into the Unreal Engine Project Settings and check it into source control.");
                _logger.LogInformation("- You can also delete the old .key, .csr and .cer files as they are no longer required.");
                _logger.LogInformation("- Don't forget to create a provisioning profile for your application using the new certificate, which can be done from: https://developer.apple.com/account/resources/profiles/list. After you create the provisioning profile, import it into the Unreal Engine Project Settings and check it into source control.");
                return 0;
            }
        }
    }
}
