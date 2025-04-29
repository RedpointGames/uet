﻿namespace UET.Commands.Internal.InstallPlatformSdk
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.SdkManagement;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class InstallXcodeCommand
    {
        public sealed class Options
        {
            public Option<string> Version;

            public Options()
            {
                Version = new Option<string>(
                    name: "--version",
                    description: "The version of Xcode to install from the `UET_APPLE_XCODE_STORAGE_PATH` folder.")
                {
                    IsRequired = true,
                };
            }
        }

        public static Command CreateInstallXcodeCommand()
        {
            var options = new Options();
            var command = new Command("install-xcode");
            command.AddAllOptions(options);
            command.AddCommonHandler<InstallXcodeCommandInstance>(options);
            return command;
        }

        private sealed class InstallXcodeCommandInstance : ICommandInstance
        {
            private readonly Options _options;
            private readonly ILogger<InstallXcodeCommandInstance> _logger;
            private readonly IEnumerable<ISdkSetup> _sdkSetups;
            private readonly IProcessExecutor _processExecutor;
            private readonly IPathResolver _pathResolver;

            public InstallXcodeCommandInstance(
                Options options,
                ILogger<InstallXcodeCommandInstance> logger,
                IEnumerable<ISdkSetup> sdkSetups,
                IProcessExecutor processExecutor,
                IPathResolver pathResolver)
            {
                _options = options;
                _logger = logger;
                _sdkSetups = sdkSetups;
                _processExecutor = processExecutor;
                _pathResolver = pathResolver;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                if (!OperatingSystem.IsMacOS())
                {
                    _logger.LogError("This command can only be run on macOS.");
                    return 1;
                }

                var version = context.ParseResult.GetValueForOption(_options.Version);
                if (string.IsNullOrWhiteSpace(version))
                {
                    Console.Error.WriteLine("error: --version must be set to the Xcode version to install.");
                    return 1;
                }

                var macSdkSetup = _sdkSetups.OfType<MacSdkSetup>().First();
                var sdkPackagePath = Path.Combine(UetPaths.UetDefaultMacSdkStoragePath, $"Mac-{version}-iOS");

                await macSdkSetup.InstallXcode(
                    version,
                    sdkPackagePath,
                    context.GetCancellationToken());

                var runtimeEnvironment = await macSdkSetup.GetRuntimeEnvironmentForSdkPackage(
                    sdkPackagePath,
                    context.GetCancellationToken());

                // Select this Xcode install as the default.
                _logger.LogInformation("Setting this Xcode install as the default with 'xcode-select'...");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("xcode-select"),
                        Arguments = new LogicalProcessArgument[]
                        {
                            "-s",
                            Path.Combine(sdkPackagePath, "Xcode.app"),
                        }
                    },
                    CaptureSpecification.Passthrough,
                    context.GetCancellationToken()).ConfigureAwait(false);

                return 0;
            }
        }
    }
}
