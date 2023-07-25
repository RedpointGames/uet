namespace Redpoint.Uet.SdkManagement
{
    using Redpoint.ProcessExecution;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using Microsoft.Extensions.Logging;
    using System.Net;
    using System.Text;

    [SupportedOSPlatform("macos")]
    public class MacSdkSetup : ISdkSetup
    {
        private readonly ILogger<MacSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;

        public MacSdkSetup(
            ILogger<MacSdkSetup> logger,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _processExecutor = processExecutor;
        }

        public string[] PlatformNames => new[] { "Mac", "IOS" };

        public string CommonPlatformNameForPackageId => "Mac";

        internal static Task<string> ParseXcodeVersion(string applePlatformSdk)
        {
            var regex = new Regex("return \"([0-9\\.]+)\"");
            foreach (Match match in regex.Matches(applePlatformSdk))
            {
                // It's the first one because GetMainVersion() is
                // the first function in this file.
                return Task.FromResult(match.Groups[1].Value);
            }
            throw new InvalidOperationException("Unable to find Clang version in ApplePlatformSDK.Versions.cs");
        }

        private async Task<string> GetXcodeVersion(string unrealEnginePath)
        {
            var applePlatformSdk = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Mac",
                "ApplePlatformSDK.Versions.cs"));
            return await ParseXcodeVersion(applePlatformSdk);
        }

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            return await GetXcodeVersion(unrealEnginePath);
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            // Check that the required environment variables have been set.
            var appleXcodeStoragePath = Environment.GetEnvironmentVariable("UET_APPLE_XCODE_STORAGE_PATH");
            if (string.IsNullOrWhiteSpace(appleXcodeStoragePath))
            {
                throw new SdkSetupMissingAuthenticationException("You must set the UET_APPLE_XCODE_STORAGE_PATH environment variable, which is the path to the mounted network share where Xcode .xip files are being stored after you have manually downloaded them from the Apple Developer portal.");
            }

            var xcodeVersion = await GetXcodeVersion(unrealEnginePath);
            var xipPath = Path.Combine(appleXcodeStoragePath, $"Xcode_{xcodeVersion}.xip");
            if (!File.Exists(xipPath))
            {
                throw new SdkSetupPackageGenerationFailedException($"Expected Xcode XIP to be present at: {xipPath}");
            }

            // Check that sudo does not require a password.
            var exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/sudo",
                    Arguments = new[]
                    {
                        "-n",
                        "true"
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
            if (exitCode != 0)
            {
                throw new SdkSetupMissingAuthenticationException($"This machine is not configured to allow 'sudo' without a password. Use 'sudo visudo' and add '{Environment.GetEnvironmentVariable("USERNAME")} ALL=(ALL) NOPASSWD: ALL' as the final line of that file.");
            }

            // Ensure Homebrew is installed.
            if (!File.Exists("/opt/homebrew/bin/brew"))
            {
                _logger.LogInformation("Installing Homebrew...");
                var homebrewScriptPath = $"/tmp/homebrew-install-{Process.GetCurrentProcess().Id}.sh";
                using (var client = new HttpClient())
                {
                    var homebrewScript = await client.GetStringAsync("https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh");
                    await File.WriteAllTextAsync(homebrewScriptPath, homebrewScript.Replace("\r\n", "\n"));
                    File.SetUnixFileMode(homebrewScriptPath,
                        UnixFileMode.UserRead |
                        UnixFileMode.UserWrite |
                        UnixFileMode.UserExecute);
                }

                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/bin/bash",
                        Arguments = new[]
                        {
                            homebrewScriptPath
                        }
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new SdkSetupPackageGenerationFailedException("Failed to install Homebrew.");
                }
            }

            // Make sure xcodes is installed.
            if (!File.Exists("/opt/homebrew/bin/xcodes"))
            {
                _logger.LogInformation("Installing xcodes...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/opt/homebrew/bin/brew",
                        Arguments = new[]
                        {
                            "install",
                            "xcodesorg/made/xcodes"
                        }
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new SdkSetupPackageGenerationFailedException("Homebrew was unable to install xcodes, which is required to automate the download and install of Xcode.");
                }
            }

            // Make sure aria2c is installed.
            if (!File.Exists("/opt/homebrew/bin/aria2c"))
            {
                _logger.LogInformation("Installing aria2c...");
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = "/opt/homebrew/bin/brew",
                        Arguments = new[]
                        {
                            "install",
                            "aria2"
                        }
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    throw new SdkSetupPackageGenerationFailedException("Homebrew was unable to install aria2, which is required to automate the download and install of Xcode.");
                }
            }

            // Install XIP.
            exitCode = await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "/usr/bin/sudo",
                    Arguments = new[]
                    {
                        "/opt/homebrew/bin/xcodes",
                        "install",
                        "--directory",
                        sdkPackagePath,
                        "--path",
                        xipPath,
                        // @note: This is turned off because it seems to be extremely brittle in non-interactive scenarios.
                        // "--experimental-unxip",
                    },
                },
                CaptureSpecification.Passthrough,
                cancellationToken);
            if (exitCode != 0)
            {
                throw new SdkSetupPackageGenerationFailedException("xcodes was unable to download and install Xcode to the SDK package directory.");
            }

            // Create a symbolic link so we can execute it more easily.
            var xcodeDirectory = Directory.GetDirectories(sdkPackagePath)
                .Select(x => Path.GetFileName(x))
                .Where(x => x.StartsWith("Xcode"))
                .First();
            File.CreateSymbolicLink(
                Path.Combine(sdkPackagePath, "Xcode.app"),
                xcodeDirectory);
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
                    Arguments = new[]
                    {
                        "/usr/bin/xcodebuild",
                        "-license",
                        "accept"
                    },
                    EnvironmentVariables = new Dictionary<string, string>()
                    {
                        { "DEVELOPER_DIR", Path.Combine(sdkPackagePath, "Xcode.app") }
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken);

            // Emit the environment variable required to use Xcode from the package directory.
            return new EnvironmentForSdkUsage
            {
                EnvironmentVariables = new Dictionary<string, string>
                {
                    { "DEVELOPER_DIR", Path.Combine(sdkPackagePath, "Xcode.app") }
                }
            };
        }
    }
}