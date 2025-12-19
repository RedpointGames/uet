namespace UET.Commands.Internal.InstallPackage
{
    using Redpoint.CommandLine;
    using Redpoint.PackageManagement;
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class InstallPackageCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<InstallPackageCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("install-package");
                })
            .Build();

        public sealed class Options
        {
            public Argument<string> Package;

            public Options()
            {
                Package = new Argument<string>(
                    name: "id",
                    description: "The package ID in WinGet or Homebrew.")
                {
                    Arity = ArgumentArity.ExactlyOne
                };
            }
        }

        private sealed class InstallPackageCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly IPackageManager _packageManager;

            public InstallPackageCommandInstance(
                Options options,
                IPackageManager packageManager)
            {
                _options = options;
                _packageManager = packageManager;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var package = context.ParseResult.GetValueForArgument(_options.Package)!;

                await _packageManager.InstallOrUpgradePackageToLatestAsync(package, context.GetCancellationToken());
                return 0;
            }
        }
    }
}
