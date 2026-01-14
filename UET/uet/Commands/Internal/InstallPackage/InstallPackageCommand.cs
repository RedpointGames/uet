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
            public Argument<string[]> Packages;

            public Options()
            {
                Packages = new Argument<string[]>(
                    name: "ids",
                    description: "One or more package IDs in WinGet or Homebrew to install.")
                {
                    Arity = ArgumentArity.OneOrMore
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
                foreach (var packageId in context.ParseResult.GetValueForArgument(_options.Packages) ?? [])
                {
                    await _packageManager.InstallOrUpgradePackageToLatestAsync(packageId, context.GetCancellationToken());
                }

                return 0;
            }
        }
    }
}
