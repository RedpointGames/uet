namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.Core;
    using System.Collections.Concurrent;
    using System.IO.Compression;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    public class AndroidSdkSetup : ISdkSetup
    {
        private readonly ILogger<AndroidSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;

        public AndroidSdkSetup(
            ILogger<AndroidSdkSetup> logger,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _processExecutor = processExecutor;
        }

        // @note: Gradle is always backwards compatible to Java 8, but is not forward compatible with newer JDK versions.
        // Therefore we have to pick a JDK version that will be usable by the Gradle that Unreal wants to use.
        private const string _jdkVersion = "jdk-11.0.19+7";
        private const string _jdkDownloadUrl = "https://aka.ms/download-jdk/microsoft-jdk-11.0.19-windows-x64.zip";

        public string PlatformName => "Android";

        private static ConcurrentDictionary<string, Assembly> _cachedCompiles = new ConcurrentDictionary<string, Assembly>();

        internal static Task<string> ParseVersion(string androidPlatformSdk, string versionCategory)
        {
            var regex = new Regex("case \"([a-z-]+)\": return \"([a-z0-9-\\.]+)\"");
            foreach (Match match in regex.Matches(androidPlatformSdk))
            {
                if (match.Groups[1].Value == versionCategory)
                {
                    return Task.FromResult(match.Groups[2].Value);
                }
            }
            throw new InvalidOperationException($"Unable to find Android version for {versionCategory} in AndroidPlatformSDK.Versions.cs");
        }

        private async Task<(string platforms, string buildTools, string cmake, string ndk)> GetVersions(string unrealEnginePath)
        {
            var androidPlatformSdk = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Android",
                "AndroidPlatformSDK.Versions.cs"));
            return (
                await ParseVersion(androidPlatformSdk, "platforms"),
                await ParseVersion(androidPlatformSdk, "build-tools"),
                await ParseVersion(androidPlatformSdk, "cmake"),
                await ParseVersion(androidPlatformSdk, "ndk"));
        }

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var versions = await GetVersions(unrealEnginePath);
            return $"{versions.platforms}-{versions.buildTools}-{versions.cmake}-{versions.ndk}-{_jdkVersion}";
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var versions = await GetVersions(unrealEnginePath);

            if (!File.Exists(Path.Combine(sdkPackagePath, "Jdk", _jdkVersion, "bin", "java.exe")))
            {
                _logger.LogInformation("Downloading and extracting the Microsoft JDK (about 177MB)...");
                if (Directory.Exists(Path.Combine(sdkPackagePath, "Jdk")))
                {
                    await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "Jdk"), true);
                }
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(_jdkDownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    Directory.CreateDirectory(Path.Combine(sdkPackagePath, "Jdk"));
                    var archive = new ZipArchive(stream);
                    archive.ExtractToDirectory(Path.Combine(sdkPackagePath, "Jdk"));
                }
            }

            if (!File.Exists(Path.Combine(sdkPackagePath, "Sdk", "cmdline-tools", "bin")))
            {
                _logger.LogInformation("Downloading and extracting the Android cmdline-tools (about 127MB)...");
                if (Directory.Exists(Path.Combine(sdkPackagePath, "Sdk")))
                {
                    await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "Sdk"), true);
                }
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync("https://dl.google.com/android/repository/commandlinetools-win-9477386_latest.zip", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    Directory.CreateDirectory(Path.Combine(sdkPackagePath, "Sdk"));
                    var archive = new ZipArchive(stream);
                    archive.ExtractToDirectory(Path.Combine(sdkPackagePath, "Sdk"));
                }
            }

            _logger.LogInformation("Accepting all Android licenses...");
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = "C:\\WINDOWS\\system32\\cmd.exe",
                    Arguments = new[]
                    {
                        "/C",
                        "sdkmanager.bat",
                        "--licenses",
                        $"--sdk_root={Path.Combine(sdkPackagePath, "Sdk")}"
                    },
                    WorkingDirectory = Path.Combine(sdkPackagePath, "Sdk", "cmdline-tools", "bin"),
                    StdinData = "y\ny\ny\ny\ny\ny\ny\ny\ny\n",
                    EnvironmentVariables = new Dictionary<string, string>
                    {
                        { "ANDROID_HOME", Path.Combine(sdkPackagePath, "Sdk") },
                        { "NDKROOT", Path.Combine(sdkPackagePath, "Sdk", "ndk", versions.ndk) },
                        { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", _jdkVersion) },
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken);

            _logger.LogInformation("Installing required Android components...");
            var components = new (string path, string componentId)[]
            {
                ($"platforms\\{versions.platforms}", $"platforms;{versions.platforms}"),
                ($"ndk\\{versions.ndk}", $"ndk;{versions.ndk}"),
                ($"build-tools\\{versions.buildTools}", $"build-tools;{versions.buildTools}"),
                ($"platform-tools", $"platform-tools"),
                ($"cmdline-tools\\latest", $"cmdline-tools;latest")
            };
            foreach (var component in components)
            {
                if (!Directory.Exists(Path.Combine(sdkPackagePath, "Sdk", component.path)))
                {
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = "C:\\WINDOWS\\system32\\cmd.exe",
                            Arguments = new[]
                            {
                                "/C",
                                "sdkmanager.bat",
                                $"--sdk_root={Path.Combine(sdkPackagePath, "Sdk")}",
                                component.componentId
                            },
                            WorkingDirectory = Path.Combine(sdkPackagePath, "Sdk", "cmdline-tools", "bin"),
                            StdinData = "y\ny\ny\ny\ny\ny\ny\ny\ny\n",
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "ANDROID_HOME", Path.Combine(sdkPackagePath, "Sdk") },
                                { "NDKROOT", Path.Combine(sdkPackagePath, "Sdk", "ndk", versions.ndk) },
                                { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", _jdkVersion) },
                            }
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
            }
            File.WriteAllText(Path.Combine(sdkPackagePath, "ndk-version.txt"), versions.ndk);
            File.WriteAllText(Path.Combine(sdkPackagePath, "jre-version.txt"), _jdkVersion);
        }

        public Task<EnvironmentForSdkUsage> EnsureSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            var ndkVersion = File.ReadAllText(Path.Combine(sdkPackagePath, "ndk-version.txt")).Trim();
            var jreVersion = File.ReadAllText(Path.Combine(sdkPackagePath, "jre-version.txt")).Trim();

            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = new Dictionary<string, string>
                {
                    { "ANDROID_HOME", Path.Combine(sdkPackagePath, "Sdk") },
                    { "NDKROOT", Path.Combine(sdkPackagePath, "Sdk", "ndk", ndkVersion) },
                    { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", jreVersion) },
                }
            });
        }
    }
}
