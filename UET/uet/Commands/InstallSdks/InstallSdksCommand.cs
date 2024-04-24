namespace UET.Commands.InstallSdks
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.CommandLine;
    using System.Linq;
    using System.Text.Json.Serialization;
    using System.Text.Json;
    using System.Threading.Tasks;
    using UET.Commands.EngineSpec;
    using Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk;
    using Redpoint.Uet.Core.Permissions;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.CommandLine;

    internal static class InstallSdksCommand
    {
        internal sealed class Options
        {
            public Option<EngineSpec> Engine;
            public Option<string[]> ConsolePlatforms;

            public Options()
            {
                Engine = new Option<EngineSpec>(
                    "--engine",
                    description: "The engine to install all of the available SDKs for.",
                    parseArgument: EngineSpec.ParseEngineSpecWithoutPath);
                Engine.AddAlias("-e");
                Engine.Arity = ArgumentArity.ExactlyOne;
                Engine.IsRequired = true;

                ConsolePlatforms = new Option<string[]>(
                    "--console",
                    description: "Map an additional platform using a confidential platform JSON file, specified in the form 'PlatformName=Path/To/Json/File.json'. You can pass this option multiple times.");
                ConsolePlatforms.AddAlias("-c");
                ConsolePlatforms.Arity = ArgumentArity.ZeroOrMore;
            }
        }

        public static ICommandLineBuilder RegisterInstallSdksCommand(this ICommandLineBuilder rootBuilder)
        {
            rootBuilder.AddCommand<InstallSdksCommandInstance, Options>(
                _ =>
                {
                    return new Command("install-sdks", "Install the platform SDKs required for a particular engine version, and set up environment variables on the local machine to use them.");
                });
            return rootBuilder;
        }

        private sealed class InstallSdksCommandInstance : ICommandInstance
        {
            private readonly ILogger<InstallSdksCommandInstance> _logger;
            private readonly Options _options;
            private readonly ILocalSdkManager _localSdkManager;
            private readonly IServiceProvider _serviceProvider;
            private readonly IWorldPermissionApplier _worldPermissionApplier;

            public InstallSdksCommandInstance(
                ILogger<InstallSdksCommandInstance> logger,
                Options options,
                ILocalSdkManager localSdkManager,
                IServiceProvider serviceProvider,
                IWorldPermissionApplier worldPermissionApplier)
            {
                _logger = logger;
                _options = options;
                _localSdkManager = localSdkManager;
                _serviceProvider = serviceProvider;
                _worldPermissionApplier = worldPermissionApplier;
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
                    _logger.LogError("You must specify a local engine (by version number or by path) in order ot use the 'install-sdks' command.");
                    return 1;
                }

                var sdkSetups = _serviceProvider.GetServices<ISdkSetup>().ToList();
                foreach (var configEntry in context.ParseResult.GetValueForOption(_options.ConsolePlatforms) ?? Array.Empty<string>())
                {
                    var kv = configEntry.Split('=', 2, StringSplitOptions.TrimEntries);
                    if (kv.Length != 2)
                    {
                        _logger.LogWarning($"The console platform specifier '{configEntry}' is not in a valid format. It will be ignored.");
                        continue;
                    }
                    if (!File.Exists(kv[1]))
                    {
                        _logger.LogWarning($"The confidential platform JSON file does not exist at path '{kv[1]}'. It will be ignored.");
                        continue;
                    }
                    var config = JsonSerializer.Deserialize(
                        File.ReadAllText(kv[1]),
                        new ConfidentialPlatformJsonSerializerContext(new JsonSerializerOptions
                        {
                            Converters =
                            {
                                new JsonStringEnumConverter(),
                            }
                        }).ConfidentialPlatformConfig)!;
                    sdkSetups.Add(
                        new ConfidentialSdkSetup(
                            kv[0],
                            config!,
                            _serviceProvider.GetRequiredService<IProcessExecutor>(),
                            _serviceProvider.GetRequiredService<IStringUtilities>(),
                            _serviceProvider.GetRequiredService<WindowsSdkInstaller>(),
                            _serviceProvider.GetRequiredService<ILogger<ConfidentialSdkSetup>>()));
                }

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

                _logger.LogInformation("Updating permissions on SDK directories so all users have read/write access...");
                await _worldPermissionApplier.GrantEveryonePermissionAsync(packagePath, context.GetCancellationToken()).ConfigureAwait(false);

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
