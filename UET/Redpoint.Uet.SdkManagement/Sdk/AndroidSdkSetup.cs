namespace Redpoint.Uet.SdkManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.PathResolution;
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
        private readonly IPathResolver _pathResolver;

        public AndroidSdkSetup(
            ILogger<AndroidSdkSetup> logger,
            IProcessExecutor processExecutor,
            IVersionNumberResolver versionNumberResolver,
            IPathResolver pathResolver)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _versionNumberResolver = versionNumberResolver;
            _pathResolver = pathResolver;
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

        public async Task<EnvironmentForSdkUsage> GetRuntimeEnvironmentForSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            var ndkVersion = File.ReadAllText(Path.Combine(sdkPackagePath, "ndk-version.txt")).Trim();
            var jreVersion = File.ReadAllText(Path.Combine(sdkPackagePath, "jre-version.txt")).Trim();

            var adbkeyPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".android",
                "adbkey");
            var adbkeyPubPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".android",
                "adbkey.pub");

            // @note: This is necessary on build servers where users and machines might vary over time.
            var privateKey =
                """
                -----BEGIN PRIVATE KEY-----
                MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQDxzwYvn11zEXM4
                yuWLzGp+C0XeQkUU6j3sJsK0bw7LRjSyR1vjAkoUJP1WMI5fy7OzzBPsg81YcODg
                Y+DnYpaJLcG8R0GWTsZbMTG29VVraRcmDz7IUyLhzn7dA5Hc9+4gGZLfg5YmO0Dd
                MViPAnJ0gdhqYbH4t9m3l1v5fDb+i3BAUKc5pSGw5jg4mRDWNj+vreWKHWx1i4bR
                sOJbxKsTe9PXznTHwRQwtkazxPR994GPIe/lYOxYaYQqqgR0nhdH5nMJgPDPeSOD
                bq9YeCPRw9yYUMa+n7cnSJ2ZMndMqiqHiYkDKGeuH2lCvsf8IT2QZD01+JEXbEKD
                hK5zH/8dAgMBAAECggEABY5B4GC7OyxUtyKURRLAhJ5aL9nab/lUzGL0mMQvdRSr
                G8h/cDcKgC18Y5lQgBt0SMaA06+QjX5kcEtjLLXLayHxwFNrypoLPSejcoZu/L2I
                ol95zAz68XC24fmVxZutrS+hPADwN3cnjZ13YSvHeO1NzV3qwqHovurbml8T/WOn
                1P+ISB4jtBuLeoRMfDKSZZDfljnaS+fk1ExRWFBMdbd2EqXQoII74oXjUL+6WblP
                ZAMUduB02cLxaPwndQ8UYcxR/PGq+hPZza8k8yfJ8MXklkl/QtxOcljOe/Om7+5R
                DKMD6dB6Jmd9QlfcJ+8U53/F4VXxYXja0YQBjJOV2QKBgQD49GZhaG+MsLKtepfD
                leuq1hg+Rwu83yu7hyO5WKOHu82RZQjvizNSnxKx0NXv0L32cVLP0G3zDXI98lmk
                C1/r7ExUOC2ECV95yOrXPjkJoFN3lkxKd0myxalUypfADtXqRMPtfjEnwcpGPwig
                wsdG+lA5h7r+ajfT1RjIOkfV9QKBgQD4ptqKkBCBtF/8eMMIkNODL/3C9SCktHob
                QCfOlZ2K4QYQyRK+2+yD2jBhpWxjK7BHhfK/WcawUcJT9f9KD3f8B4mqUADEweER
                eWh90O3yRYd0k35V4SVBbuIAYRdwzHiS5WFVKYQyuYRUpRP1h5pXZWGarYxLqtO6
                vhe7G/YjiQKBgDUFqIB6g7eNMqDsCUKovYanDobFDuTtCx1njN4+2KViBEhBIoQS
                O54PLyYb+lSXOr4wKJkGJUSsynYTFbBwk79llmQhiuAiNulzN0EciX1ZXi2MHzeE
                7HdczdG3TFalUj4Q40HDrKhxB6mqZyYGFfcx/MAj/lmNOdKuAhczAnW5AoGAQyet
                NmcaTi2NDv7+jb2vomq/unvByToFEH8PQTgfSHbl0Hq92VZEVogDMRwgXdhaz7ZZ
                jVyN0OkD9vEldbcfzK2sfJcG3h0O0E1d7z0SRrCImO+M21znVvi/iSKv1gMjPWk+
                FGYWEi0QlFvRPCrXgGsdJU1h6r3EWVclyZ8PpyECgYEA87IZZCYXgvsGUz9sQSth
                OQKEBE0OWnDHs1ZrMs0lfxKImPWrbj7CdKO5QlJADMwyH6nEL3ZVb8ETDK6T1VZa
                Fbs6Pe7QRnoJucsM01pC79w0UtnNCHdXEEVeAbdKJmYgbGYU0Uvcsnz3M45lHsCy
                T758Kl3IJGbsGU3C/T6WQUM=
                -----END PRIVATE KEY-----
                """.Replace("\r\n", "\n", StringComparison.Ordinal);
            var publicKey =
                """
                QAAAAMtEwZUd/x9zroSDQmwXkfg1PWSQPSH8x75CaR+uZygDiYmHKqpMdzKZnUgnt5++xlCY3MPRI3hYr26DI3nP8IAJc+ZHF550BKoqhGlY7GDl7yGPgfd99MSzRrYwFMHHdM7X03sTq8Rb4rDRhot1bB2K5a2vPzbWEJk4OOawIaU5p1BAcIv+Nnz5W5e32bf4sWFq2IF0cgKPWDHdQDsmloPfkhkg7vfckQPdfs7hIlPIPg8mF2lrVfW2MTFbxk6WQUe8wS2JlmLn4GPg4HBYzYPsE8yzs8tfjjBW/SQUSgLjW0eyNEbLDm+0wibsPeoURULeRQt+asyL5co4cxFzXZ8vBs/xr6bxkIoKf8rjRNqozj5iXXPhLiW0euQNbFyxwMaGU2j/5phWQ3M5jyUWhsJ4iZb1+m8qyIqgjxfVnnBQlJZdH9ytkERWRx905AE2ITStDL5JUFYrl7hlNgArhdhUosBpVbl/a1DbP4EDaqeFhgfuCy77/I0SoAm5XgMnfqIv3w+tBR8kPGh1sJKxkICXqGK5Zhiyag3BIfLz3eskB1XZdrFn8JrvRsj3ZQwpxDGTtlw2lsq3FbKlVWMucyqbNTnBiuI/Vd10HqY6oaLvGKMbbwnr8eQS3x/0T0vn0b30KEibLAjPwaomuFWq6UdvIGY1RWlEqS93PUtPpIki3vqQkQEAAQA= uet-well-known-key
                """.Replace("\r\n", "\n", StringComparison.Ordinal);

            var existingPrivateKey = File.Exists(adbkeyPath) ? File.ReadAllText(adbkeyPath) : string.Empty;
            var existingPublicKey = File.Exists(adbkeyPubPath) ? File.ReadAllText(adbkeyPubPath) : string.Empty;

            if (existingPrivateKey != privateKey || existingPublicKey != publicKey)
            {
                _logger.LogInformation("Forcing adbkey to be a well-known key to avoid USB re-authorization prompts...");
                File.WriteAllText(adbkeyPath, privateKey);
                File.WriteAllText(adbkeyPubPath, publicKey);

                _logger.LogInformation("Terminating any existing 'adb' processes to ensure ADB server sees new public/private keypair...");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = await _pathResolver.ResolveBinaryPath("taskkill").ConfigureAwait(false),
                        Arguments = ["/f", "/im", "adb.exe"]
                    },
                    CaptureSpecification.Passthrough,
                    CancellationToken.None).ConfigureAwait(false);
            }

            return new EnvironmentForSdkUsage
            {
                EnvironmentVariables = new Dictionary<string, string>
                {
                    { "ANDROID_HOME", Path.Combine(sdkPackagePath, "Sdk") },
                    { "NDKROOT", Path.Combine(sdkPackagePath, "Sdk", "ndk", ndkVersion) },
                    { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", jreVersion) },

                    // Use the openscreen library to allow ADB to discover devices via mDNS without Bonjour installed.
                    { "ADB_MDNS_OPENSCREEN", "1" },
                }
            };
        }
    }
}
