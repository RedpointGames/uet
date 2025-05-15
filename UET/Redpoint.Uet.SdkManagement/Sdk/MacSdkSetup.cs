namespace Redpoint.Uet.SdkManagement
{
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;
    using System.Runtime.Versioning;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProgressMonitor;
    using System.IO;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using Redpoint.PackageManagement;

    [SupportedOSPlatform("macos")]
    public class MacSdkSetup : ISdkSetup
    {
        private readonly ILogger<MacSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IProgressFactory _progressFactory;
        private readonly IMonitorFactory _monitorFactory;
        private readonly IVersionNumberResolver _versionNumberResolver;
        private readonly IPackageManager _packageManager;

        public MacSdkSetup(
            ILogger<MacSdkSetup> logger,
            IProcessExecutor processExecutor,
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory,
            IVersionNumberResolver versionNumberResolver,
            IPackageManager packageManager)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _progressFactory = progressFactory;
            _monitorFactory = monitorFactory;
            _versionNumberResolver = versionNumberResolver;
            _packageManager = packageManager;
        }

        public IReadOnlyList<string> PlatformNames => new[] { "Mac", "IOS" };

        public string CommonPlatformNameForPackageId => "Mac";

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var versionNumber = await _versionNumberResolver.For<IMacVersionNumbers>(unrealEnginePath).GetXcodeVersion(unrealEnginePath).ConfigureAwait(false);
            return $"{versionNumber}-iOS";
        }

        public async Task InstallXcode(string xcodeVersion, string sdkPackagePath, CancellationToken cancellationToken)
        {
            // Check that the required environment variables have been set.
            var appleXcodeStoragePath = Environment.GetEnvironmentVariable("UET_APPLE_XCODE_STORAGE_PATH");
            if (string.IsNullOrWhiteSpace(appleXcodeStoragePath))
            {
                throw new SdkSetupMissingAuthenticationException("You must set the UET_APPLE_XCODE_STORAGE_PATH environment variable, which is the path to the mounted network share where Xcode .xip files are being stored after you have manually downloaded them from the Apple Developer portal.");
            }

            var xipSourcePath = Path.Combine(appleXcodeStoragePath, $"Xcode_{xcodeVersion}.xip");
            if (!File.Exists(xipSourcePath))
            {
                throw new SdkSetupPackageGenerationFailedException($"Expected Xcode XIP to be present at: {xipSourcePath}");
            }

            // Check that sudo does not require a password.
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/sudo",
                    Arguments = new LogicalProcessArgument[]
                    {
                        "-n",
                        "true"
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                var username = Environment.GetEnvironmentVariable("USERNAME");
                if (string.IsNullOrWhiteSpace(username))
                {
                    username = Environment.UserName;
                }
                throw new SdkSetupMissingAuthenticationException($"This machine is not configured to allow 'sudo' without a password. Use 'sudo visudo' and add '{username} ALL=(ALL) NOPASSWD: ALL' as the final line of that file.");
            }

            // Ensure xcodes is installed.
            await _packageManager.InstallOrUpgradePackageToLatestAsync("xcodesorg/made/xcodes", cancellationToken);

            // Ensure aria2 is installed.
            await _packageManager.InstallOrUpgradePackageToLatestAsync("aria2", cancellationToken);

            // We must copy the XIP to the target directory, since XIP files will
            // always be extracted next to the .xip file, regardless of the
            // destination. We obviously don't want that to happen on a network
            // share.
            _logger.LogInformation($"Copying {Path.GetFileName(xipSourcePath)} from network share...");
            var xipPath = Path.Combine(sdkPackagePath, Path.GetFileName(xipSourcePath));
            using (var source = new FileStream(xipSourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var destination = new FileStream(xipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    // Start monitoring.
                    var cts = new CancellationTokenSource();
                    var progress = _progressFactory.CreateProgressForStream(source);
                    var monitorTask = Task.Run(async () =>
                    {
                        var monitor = _monitorFactory.CreateByteBasedMonitor();
                        await monitor.MonitorAsync(
                            progress,
                            SystemConsole.ConsoleInformation,
                            SystemConsole.WriteProgressToConsole,
                            cts.Token).ConfigureAwait(false);
                    }, cts.Token);

                    // Copy the data.
                    await source.CopyToAsync(destination, 2 * 1024 * 1024, cancellationToken).ConfigureAwait(false);

                    // Stop monitoring.
                    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts).ConfigureAwait(false);
                }
            }
            try
            {
                // Install XIP.
                _logger.LogInformation($"Installing Xcode {xcodeVersion} using 'xcodes'...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/usr/bin/sudo",
                        Arguments = new LogicalProcessArgument[]
                        {
                            "/opt/homebrew/bin/xcodes",
                            "install",
                            xcodeVersion,
                            "--directory",
                            sdkPackagePath,
                            "--path",
                            xipPath,
                            // @note: This is turned off because it seems to be extremely brittle in non-interactive scenarios.
                            // "--experimental-unxip",
                        },
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(false);
                if (exitCode != 0)
                {
                    throw new SdkSetupPackageGenerationFailedException("xcodes was unable to download and install Xcode to the SDK package directory.");
                }

                // Create a symbolic link so we can execute it more easily.
                _logger.LogInformation($"Setting up symbolic link for Xcode.app...");
                var xcodeDirectory = Directory.GetDirectories(sdkPackagePath)
                    .Select(x => Path.GetFileName(x))
                    .First(x => x.StartsWith("Xcode", StringComparison.Ordinal));
                File.CreateSymbolicLink(
                    Path.Combine(sdkPackagePath, "Xcode.app"),
                    xcodeDirectory);
            }
            finally
            {
                _logger.LogInformation($"Removing temporary .xip file to reduce disk space...");
                File.Delete(xipPath);
            }

            // Perform first run.
            _logger.LogInformation("Performing Xcode first-run...");
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/xcodebuild",
                    Arguments = new LogicalProcessArgument[]
                    {
                        "-runFirstLaunch"
                    },
                    EnvironmentVariables = new Dictionary<string, string>
                    {
                        { "DEVELOPER_DIR", Path.Combine(sdkPackagePath, "Xcode.app") },
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new SdkSetupPackageGenerationFailedException("Xcode was unable to perform first-run launch.");
            }

            // Install iOS platform if needed.
            _logger.LogInformation("Installing iOS platform...");
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/xcodebuild",
                    Arguments = new LogicalProcessArgument[]
                    {
                        "-downloadPlatform",
                        "iOS"
                    },
                    EnvironmentVariables = new Dictionary<string, string>
                    {
                        { "DEVELOPER_DIR", Path.Combine(sdkPackagePath, "Xcode.app") },
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            if (exitCode != 0)
            {
                throw new SdkSetupPackageGenerationFailedException("Xcode was unable to install iOS platform support.");
            }
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var xcodeVersion = await _versionNumberResolver.For<IMacVersionNumbers>(unrealEnginePath).GetXcodeVersion(unrealEnginePath).ConfigureAwait(false);
            await InstallXcode(xcodeVersion, sdkPackagePath, cancellationToken);
        }

        public Task<AutoSdkMapping[]> GetAutoSdkMappingsForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<AutoSdkMapping>());
        }

        public async Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            // Accept the Xcode license agreement on this machine.
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/sudo",
                    Arguments = new LogicalProcessArgument[]
                    {
                        Path.Combine(sdkPackagePath, "Xcode.app", "Contents", "Developer", "usr", "bin", "xcodebuild"),
                        "-license",
                        "accept"
                    },
                    EnvironmentVariables = new Dictionary<string, string>()
                    {
                        { "DEVELOPER_DIR", Path.Combine(sdkPackagePath, "Xcode.app") }
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);

            // Emit the environment variable required to use Xcode from the package directory.
            var envs = new Dictionary<string, string>
            {
                { "DEVELOPER_DIR", Path.Combine(sdkPackagePath, "Xcode.app") }
            };
            var currentPath = Environment.GetEnvironmentVariable("PATH");
            if (currentPath != null)
            {
                envs["PATH"] = string.Join(
                    Path.PathSeparator,
                    new[]
                    {
                        Path.Combine(sdkPackagePath, "Xcode.app", "Contents", "Developer", "usr", "bin"),
                        Path.Combine(sdkPackagePath, "Xcode.app", "Contents", "Developer", "usr", "libexec"),
                        currentPath
                    });
            }
            return new EnvironmentForSdkUsage
            {
                EnvironmentVariables = envs
            };
        }
    }
}