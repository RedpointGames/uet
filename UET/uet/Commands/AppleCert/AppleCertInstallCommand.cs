namespace UET.Commands.AppleCert
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Uet.BuildPipeline.BuildGraph.MobileProvisioning;
    using Redpoint.Uet.Configuration.Engine;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal sealed class AppleCertInstallCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        internal sealed class Options
        {
            public Option<DirectoryInfo> StoragePath;

            public Options()
            {
                StoragePath = new Option<DirectoryInfo>("--storage-path")
                {
                    Description = "The path that contains certificate and mobile provisioning files. If not set, defaults to the current directory.",
                };
                StoragePath.AddAlias("-s");
            }
        }

        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<CreateAppleCertInstallCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("install", "Install all certificate and mobile provision files so they can be used by the Unreal Engine build system.");
                    builder.GlobalContext.CommandRequiresUetVersionInBuildConfig(command);
                    return command;
                })
            .Build();

        private sealed class CreateAppleCertInstallCommandInstance : ICommandInstance
        {
            private readonly ILogger<CreateAppleCertInstallCommandInstance> _logger;
            private readonly IMobileProvisioning _mobileProvisioning;
            private readonly Options _options;

            public CreateAppleCertInstallCommandInstance(
                ILogger<CreateAppleCertInstallCommandInstance> logger,
                IMobileProvisioning mobileProvisioning,
                Options options)
            {
                _logger = logger;
                _mobileProvisioning = mobileProvisioning;
                _options = options;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var storagePath = context.ParseResult.GetValueForOption(_options.StoragePath);

                // Default the storage path to the current directory, otherwise ensure the storage path exists.
                if (storagePath == null)
                {
                    storagePath = new DirectoryInfo(Environment.CurrentDirectory);
                }
                else
                {
                    storagePath.Create();
                }

                // Iterate through .mobileprovision files, and build the list of provisions to install.
                var mobileProvisions = new List<BuildConfigMobileProvision>();
                _logger.LogInformation($"Scanning '{storagePath}' for mobile provisioning files...");
                foreach (var file in storagePath.GetFiles("*.mobileprovision"))
                {
                    var p12File = Path.Combine(storagePath.FullName, Path.GetFileNameWithoutExtension(file.Name) + ".p12");
                    var cerFile = Path.Combine(storagePath.FullName, Path.GetFileNameWithoutExtension(file.Name) + ".cer");
                    if (File.Exists(p12File))
                    {
                        _logger.LogInformation($"Found '{file.Name}'.");
                        mobileProvisions.Add(new BuildConfigMobileProvision
                        {
                            BundleIdentifierPattern = null,
                            KeychainPasswordEnvironmentVariable = null,
                            MobileProvisionPath = file.FullName,
                            PrivateKeyPasswordlessP12Path = p12File,
                            AppleProvidedCertificatePath = File.Exists(cerFile) ? cerFile : null,
                        });
                    }
                    else
                    {
                        _logger.LogWarning($"Skipping '{file.Name}' because certificate '{p12File}' does not exist.");
                    }
                }

                // Install mobile provisions.
                _logger.LogInformation($"There are {mobileProvisions.Count} mobile provisions to install.");
                await _mobileProvisioning.InstallMobileProvisions(mobileProvisions, context.GetCancellationToken());
                return 0;
            }
        }
    }
}
