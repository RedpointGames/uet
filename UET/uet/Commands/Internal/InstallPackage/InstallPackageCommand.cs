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
            public Option<string[]> OverrideLocation;

            public Options()
            {
                Packages = new Argument<string[]>(
                    name: "ids",
                    description: "One or more package IDs in WinGet or Homebrew to install.")
                {
                    Arity = ArgumentArity.OneOrMore
                };

                OverrideLocation = new Option<string[]>(
                    name: "--override-location",
                    description: "A key-value pair in the format id=location which overrides the location a package is installed.");
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
                var overrideLocationsArray = context.ParseResult.GetValueForOption(_options.OverrideLocation) ?? [];
                var overrideLocations = new Dictionary<string, string>();
                foreach (var overrideLocation in overrideLocationsArray)
                {
                    var components = overrideLocation.Split('=', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (components.Length == 2)
                    {
                        overrideLocations[components[0]] = components[1];
                    }
                }

                foreach (var packageId in context.ParseResult.GetValueForArgument(_options.Packages) ?? [])
                {
                    await _packageManager.InstallOrUpgradePackageToLatestAsync(
                        packageId,
                        locationOverride: overrideLocations.TryGetValue(packageId, out var locationOverride) && !string.IsNullOrWhiteSpace(locationOverride)
                            ? locationOverride
                            : null,
                        cancellationToken: context.GetCancellationToken());
                }

                return 0;
            }
        }
    }
}
