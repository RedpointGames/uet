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
            var appleEmail = Environment.GetEnvironmentVariable("UET_APPLE_EMAIL");
            var applePassword = Environment.GetEnvironmentVariable("UET_APPLE_PASSWORD");
            var applePhoneNumber = Environment.GetEnvironmentVariable("UET_APPLE_PHONE_NUMBER");
            var appleTwoFactorProxyUrl = Environment.GetEnvironmentVariable("UET_APPLE_2FA_PROXY_URL")?.TrimEnd('/');

            if (string.IsNullOrWhiteSpace(appleEmail) ||
                string.IsNullOrWhiteSpace(applePassword) ||
                string.IsNullOrWhiteSpace(applePhoneNumber) ||
                string.IsNullOrWhiteSpace(appleTwoFactorProxyUrl))
            {
                throw new SdkSetupMissingAuthenticationException("You must set the UET_APPLE_EMAIL, UET_APPLE_PASSWORD, UET_APPLE_PHONE_NUMBER and UET_APPLE_2FA_PROXY_URL environment variables to authenticate an Apple account. Use 'uet internal setup-apple-two-factor-proxy' to configure a proxy for handling two-factor authentication.");
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

            // Reserve a session via the proxy for performing two-factor authentication. We reserve on the proxy so that
            // even across multiple machines, we don't have two processes trying to go through two-factor authentication.
            do
            {
                using (var client = new HttpClient())
                {
                    var sessionId = Guid.NewGuid().ToString();
                    _logger.LogWarning($"Attempting to reserve the two-factor authentication proxy using session ID '{sessionId}'...");
                    var sessionResponse = await client.PostAsync($"{appleTwoFactorProxyUrl}/session?number={applePhoneNumber}&sessionId={sessionId}", new StringContent(string.Empty));
                    var retry = false;
                    try
                    {
                        _logger.LogTrace($"Response from proxy: {await sessionResponse.Content.ReadAsStringAsync()}");
                    }
                    catch
                    {
                    }
                    switch (sessionResponse.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            // We have the session reserved.
                            break;
                        case HttpStatusCode.Conflict:
                            // Another session is currently going through two-factor authentication.
                            _logger.LogWarning("Another device is currently going through two-factor authentication. Retrying in 5 minutes...");
                            await Task.Delay(60000 * 5, cancellationToken);
                            retry = true;
                            break;
                        case HttpStatusCode.Forbidden:
                            // This phone number is incorrect.
                            throw new SdkSetupPackageGenerationFailedException("The two-factor authentication proxy denied your request, because the configured phone number was incorrect. This is a configuration issue and you must update the relevant environment variables.");
                        default:
                            throw new SdkSetupPackageGenerationFailedException($"The two-factor authentication proxy responded with the unexpected status code {sessionResponse.StatusCode} during session reservation.");
                    }
                    if (retry)
                    {
                        continue;
                    }

                    _logger.LogInformation($"Two-factor authentication proxy successfully reserved. Waiting two minutes to ensure that we are still the owner of the reservation before proceeding...");
                    await Task.Delay(60000 * 2, cancellationToken);

                    sessionResponse = await client.PostAsync($"{appleTwoFactorProxyUrl}/session?number={applePhoneNumber}&sessionId={sessionId}", new StringContent(string.Empty));
                    retry = false;
                    switch (sessionResponse.StatusCode)
                    {
                        case HttpStatusCode.OK:
                            // We still have the session reserved.
                            break;
                        case HttpStatusCode.Conflict:
                            // Another session is currently going through two-factor authentication.
                            _logger.LogWarning("Another device stole our reservation before we could proceed. Retrying in 5 minutes...");
                            await Task.Delay(60000 * 5, cancellationToken);
                            retry = true;
                            break;
                        default:
                            throw new SdkSetupPackageGenerationFailedException($"The two-factor authentication proxy responded with the unexpected status code {sessionResponse.StatusCode} during session confirmation.");
                    }
                    if (retry)
                    {
                        continue;
                    }

                    retry = false;
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                    var promptResponse = new CaptureSpecificationPromptResponse();
                    promptResponse.Add(
                        new Regex("^Enter the [0-9]+ digit code sent to [^:]+: $"),
                        async standardInput =>
                        {
                            while (true)
                            {
                                // Wait until the token comes in.
                                _logger.LogInformation("Attempting to retrieve two-factor code from proxy...");
                                var codeResponse = await client.GetAsync(
                                    $"{appleTwoFactorProxyUrl}/?number={applePhoneNumber}&sessionId={sessionId}");
                                switch (codeResponse.StatusCode)
                                {
                                    case HttpStatusCode.OK:
                                        // We have a code.
                                        _logger.LogInformation("Received two-factor code from proxy, authenticating...");
                                        standardInput.WriteLine(await codeResponse.Content.ReadAsStringAsync());
                                        return;
                                    case HttpStatusCode.NotFound:
                                        _logger.LogInformation("2FA code not received yet, waiting 5 seconds...");
                                        await Task.Delay(5 * 1000, cancellationToken);
                                        break;
                                    default:
                                        _logger.LogError($"The two-factor authentication proxy responded with the unexpected status code {sessionResponse.StatusCode} during code obtainment.");
                                        retry = false;
                                        cts.Cancel();
                                        return;
                                }
                            }
                        });
                    promptResponse.Add(
                        new Regex("^Apple ID: Missing username or a password\\. Please try again\\.$"),
                        standardInput =>
                        {
                            retry = true;
                            cts.Cancel();
                            return Task.CompletedTask;
                        });

                    _logger.LogInformation("Two-factor authentication proxy reservation confirmed. Proceeding with Xcode installation and potential two-factor authentication...");
                    try
                    {
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
                                    "--empty-trash",
                                    // @note: This is turned off because it seems to be extremely brittle in non-interactive scenarios.
                                    // "--experimental-unxip",
                                    await GetXcodeVersion(unrealEnginePath)
                                },
                                EnvironmentVariables = new Dictionary<string, string>
                                {
                                    { "XCODES_USERNAME", appleEmail },
                                    { "XCODES_PASSWORD", applePassword },
                                },
                            },
                            CaptureSpecification.CreateFromPromptResponse(promptResponse),
                            cts.Token);
                        if (exitCode != 0)
                        {
                            throw new SdkSetupPackageGenerationFailedException("xcodes was unable to download and install Xcode to the SDK package directory.");
                        }
                    }
                    catch (OperationCanceledException) when (cts.IsCancellationRequested && retry && !cancellationToken.IsCancellationRequested)
                    {
                        // We need to retry in a moment.
                        _logger.LogWarning("Detected that xcodes failed to detect authentication. Retrying in 15 seconds...");
                        await Task.Delay(15 * 1000, cancellationToken);
                        continue;
                    }

                    // We're done.
                    break;
                }
            } while (true);

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