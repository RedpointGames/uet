namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.Uet.SdkManagement.Sdk.VersionNumbers;
    using System.IO.Compression;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows")]
    public class AndroidSdkSetup : ISdkSetup
    {
        private readonly ILogger<AndroidSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IVersionNumberResolver _versionNumberResolver;

        public AndroidSdkSetup(
            ILogger<AndroidSdkSetup> logger,
            IProcessExecutor processExecutor,
            IVersionNumberResolver versionNumberResolver)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _versionNumberResolver = versionNumberResolver;
        }

        private struct JdkInfo
        {
            public required string JdkVersion;
            public required string JdkDownloadUrl;
        }

        // @note: Gradle is always backwards compatible to Java 8, but is not forward compatible with newer JDK versions.
        // Therefore we have to pick a JDK version that will be usable by the Gradle that Unreal wants to use.
        private const string _jdkInfoDefault = "17";
        private readonly Dictionary<string, JdkInfo> _jdkInfos = new()
        {
            // JDK 11 required by earlier Unreal Engine versions.
            {
                "11",
                new JdkInfo
                {
                    JdkVersion = "jdk-11.0.19+7",
                    JdkDownloadUrl = "https://aka.ms/download-jdk/microsoft-jdk-11.0.19-windows-x64.zip"
                }
            },
            // JDK 17 required by Unreal Engine 5.5.
            {
                "17",
                new JdkInfo
                {
                    JdkVersion = "jdk-17.0.13+11",
                    JdkDownloadUrl = "https://aka.ms/download-jdk/microsoft-jdk-17.0.13-windows-x64.zip"
                }
            }
            // @note: If adding a new version, also update _jdkInfoDefault!
        };

        private JdkInfo GetJdkInfo()
        {
            var jdkOverride = Environment.GetEnvironmentVariable("UET_OVERRIDE_JDK_VERSION");
            if (!string.IsNullOrWhiteSpace(jdkOverride))
            {
                if (_jdkInfos.TryGetValue(jdkOverride.Trim(), out var jdkInfo))
                {
                    return jdkInfo;
                }

                _logger.LogError($"The environment variable UET_OVERRIDE_JDK_VERSION is set to '{jdkOverride}', but this is not a supported version. Supported versions are one of: {string.Join(", ", _jdkInfos.Keys.Select(x => $"\"{x}\""))}. The environment variable will be ignored, and we'll install the latest version, which may cause Android build issues.");
            }

            if (_jdkInfos.TryGetValue(_jdkInfoDefault, out var jdkDefaultInfo))
            {
                return jdkDefaultInfo;
            }

            _logger.LogError($"Detected bug! The default JDK version is set to '{_jdkInfoDefault}' in the code, but this is not a supported version. Supported versions are one of: {string.Join(", ", _jdkInfos.Keys.Select(x => $"\"{x}\""))}. UET will have to pick a version at random.");
            return _jdkInfos.Last().Value;
        }

        public IReadOnlyList<string> PlatformNames => new[] { "Android", "GooglePlay", "MetaQuest" };

        public string CommonPlatformNameForPackageId => "Android";

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var jdkInfo = GetJdkInfo();
            var versions = await _versionNumberResolver.For<IAndroidVersionNumbers>(unrealEnginePath).GetVersions(unrealEnginePath).ConfigureAwait(false);
            return $"{versions.platforms}-{versions.buildTools}-{versions.cmake}-{versions.ndk}-{jdkInfo.JdkVersion}";
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var jdkInfo = GetJdkInfo();
            var versions = await _versionNumberResolver.For<IAndroidVersionNumbers>(unrealEnginePath).GetVersions(unrealEnginePath).ConfigureAwait(false);

            if (!File.Exists(Path.Combine(sdkPackagePath, "Jdk", jdkInfo.JdkVersion, "bin", "java.exe")))
            {
                _logger.LogInformation("Downloading and extracting the Microsoft JDK (about 177MB)...");
                if (Directory.Exists(Path.Combine(sdkPackagePath, "Jdk")))
                {
                    await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "Jdk"), true).ConfigureAwait(false);
                }
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(new Uri(jdkInfo.JdkDownloadUrl), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
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
                    await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "Sdk"), true).ConfigureAwait(false);
                }
                using (var client = new HttpClient())
                {
                    var response = await client.GetAsync(new Uri("https://dl.google.com/android/repository/commandlinetools-win-9477386_latest.zip"), HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
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
                    Arguments = new LogicalProcessArgument[]
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
                        { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", jdkInfo.JdkVersion) },
                    }
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);

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
                            Arguments = new LogicalProcessArgument[]
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
                                { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", jdkInfo.JdkVersion) },
                            }
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            File.WriteAllText(Path.Combine(sdkPackagePath, "ndk-version.txt"), versions.ndk);
            File.WriteAllText(Path.Combine(sdkPackagePath, "jre-version.txt"), jdkInfo.JdkVersion);
        }

        public Task<AutoSdkMapping[]> GetAutoSdkMappingsForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            return Task.FromResult(Array.Empty<AutoSdkMapping>());
        }

        public Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
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

                    // Use the openscreen library to allow ADB to discover devices via mDNS without Bonjour installed.
                    { "ADB_MDNS_OPENSCREEN", "1" },
                }
            });
        }
    }
}
