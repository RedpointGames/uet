namespace UET.Commands.Internal.InstallPackage
{
    using Redpoint.PackageManagement;
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class InstallPackageCommand
    {
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

        public static Command CreateInstallPackageCommand()
        {
            var options = new Options();
            var command = new Command("install-package");
            command.AddAllOptions(options);
            command.AddCommonHandler<InstallPackageCommandInstance>(options);
            return command;
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

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var package = context.ParseResult.GetValueForArgument(_options.Package)!;

                await _packageManager.InstallOrUpgradePackageToLatestAsync(package, context.GetCancellationToken());
                return 0;
            }
        }
    }
}
