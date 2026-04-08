namespace UET.Commands.InstallSdks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Commands.ParameterSpec;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.SdkManagement;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.SdkManagement.Sdk.Discovery;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Text.Json;
    using System.Text.Json.Serialization;
    using System.Threading.Tasks;

    internal sealed class InstallSdksCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<InstallSdksCommandInstance>()
            .WithCommand(
                builder =>
                {
                    var command = new Command("install-sdks", "Install the platform SDKs required for a particular engine version, and set up environment variables on the local machine to use them.");
                    builder.GlobalContext.CommandRequiresUetVersionInBuildConfig(command);
                    return command;
                })
            .Build();

        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<bool> SkipPermissionUpdate;

            public Options()
            {
                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to install all of the available SDKs for.",
                    parseArgument: EngineSpec.ParseEngineSpecWithoutPath,
                    isDefault: true);
                Engine.AddAlias("-e");

                SkipPermissionUpdate = new Option<bool>(
                    "--skip-permissions",
                    description: "Skip updating permissions on SDKs.");
                SkipPermissionUpdate.AddAlias("-s");
                SkipPermissionUpdate.SetDefaultValue(false);
            }
        }

        private sealed class InstallSdksCommandInstance : ICommandInstance
        {
            private readonly ILogger<InstallSdksCommandInstance> _logger;
            private readonly Options _options;
            private readonly ILocalSdkManager _localSdkManager;
            private readonly IWorldPermissionApplier _worldPermissionApplier;
            private readonly ISdkSetupDiscovery _sdkSetupDiscovery;

            public InstallSdksCommandInstance(
                ILogger<InstallSdksCommandInstance> logger,
                Options options,
                ILocalSdkManager localSdkManager,
                IWorldPermissionApplier worldPermissionApplier,
                ISdkSetupDiscovery sdkSetupDiscovery)
            {
                _logger = logger;
                _options = options;
                _localSdkManager = localSdkManager;
                _worldPermissionApplier = worldPermissionApplier;
                _sdkSetupDiscovery = sdkSetupDiscovery;
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                if (!OperatingSystem.IsWindows())
                {
                    _logger.LogError("This command is not currently supported on non-Windows platforms.");
                    return 1;
                }

                var engine = context.ParseResult.GetValueForOption(_options.Engine)!;

                if (engine.Path == null)
                {
                    _logger.LogError("You must specify a local engine (by version number or by path) in order to use the 'install-sdks' command.");
                    return 1;
                }

                var sdkSetups = await _sdkSetupDiscovery
                    .DiscoverApplicableSdkSetups(engine.Path!)
                    .ToListAsync();

                _logger.LogInformation("The following platforms will have their SDKs configured:");
                foreach (var platform in sdkSetups.Select(x => x.CommonPlatformNameForPackageId).ToHashSet())
                {
                    _logger.LogInformation($" - {platform}");
                }

                var packagePath = UetPaths.UetDefaultWindowsSdkStoragePath;
                Directory.CreateDirectory(packagePath);

                var envVars = await _localSdkManager.SetupEnvironmentForSdkSetups(
                    engine.Path,
                    packagePath,
                    sdkSetups.ToHashSet(),
                    context.GetCancellationToken()).ConfigureAwait(false);

                if (!context.ParseResult.GetValueForOption(_options.SkipPermissionUpdate))
                {
                    _logger.LogInformation("Updating permissions on SDK directories so all users have read/write access...");
                    await _worldPermissionApplier.GrantEveryonePermissionAsync(packagePath, context.GetCancellationToken()).ConfigureAwait(false);
                }

                _logger.LogInformation("Setting environment variables to user scope...");
                foreach (var kv in envVars)
                {
                    _logger.LogInformation($"  {kv.Key} = {kv.Value}");
                    if (Environment.GetEnvironmentVariable(kv.Key, EnvironmentVariableTarget.User) != kv.Value)
                    {
                        Environment.SetEnvironmentVariable(kv.Key, kv.Value, EnvironmentVariableTarget.User);
                    }
                    context.GetCancellationToken().ThrowIfCancellationRequested();
                }

                return 0;
            }
        }
    }
}
