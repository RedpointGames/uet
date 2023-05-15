namespace Redpoint.UET.SdkManagement
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System.Collections.Concurrent;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Versioning;
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

        public string PlatformName => "Android";

        private static ConcurrentDictionary<string, Assembly> _cachedCompiles = new ConcurrentDictionary<string, Assembly>();

        internal static async Task<string> ParseVersion(string androidPlatformSdk, string versionCategory)
        {
            Assembly targetAssembly;
            if (!_cachedCompiles.TryGetValue(androidPlatformSdk, out targetAssembly!))
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(androidPlatformSdk);
                var syntaxRoot = await syntaxTree.GetRootAsync();
                var blockCode = syntaxRoot.DescendantNodes()
                    .OfType<MethodDeclarationSyntax>()
                    .Where(x => x.Identifier.Text == "GetPlatformSpecificVersion")
                    .First()
                    .ChildNodes()
                    .OfType<BlockSyntax>()
                    .First()
                    .ToString();
                var classCode = @$"
class AndroidVersionLoader
{{
    public static string GetPlatformSpecificVersion(string VersionType)
    {blockCode}
}}
";
                var newSyntaxTree = CSharpSyntaxTree.ParseText(classCode);
                var rtPath = Path.GetDirectoryName(typeof(object).Assembly.Location) +
                             Path.DirectorySeparatorChar;
                var compilation = CSharpCompilation.Create("AndroidVersionLoader.cs")
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .WithReferences(
                        MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Private.CoreLib.dll")),
                        MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Runtime.dll")))
                    .AddSyntaxTrees(newSyntaxTree);
                using (var stream = new MemoryStream())
                {
                    var compilationResult = compilation.Emit(stream);
                    targetAssembly = Assembly.Load(stream.ToArray());
                }
                _cachedCompiles.AddOrUpdate(androidPlatformSdk, targetAssembly, (_, _) => targetAssembly);
            }

            var version = (string)targetAssembly.GetType("AndroidVersionLoader")!
                .GetMethod("GetPlatformSpecificVersion", BindingFlags.Static | BindingFlags.Public)!
                .Invoke(null, new object[] { versionCategory })!;
            return version;
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
            return $"{versions.platforms}-{versions.buildTools}-{versions.cmake}-{versions.ndk}";
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var versions = await GetVersions(unrealEnginePath);

            if (!File.Exists(Path.Combine(sdkPackagePath, "Jdk", "jdk-17.0.7+7", "bin", "java.exe")))
            {
                _logger.LogInformation("Downloading and extracting the Microsoft JDK (about 177MB)...");
                if (Directory.Exists(Path.Combine(sdkPackagePath, "Jdk")))
                {
                    Directory.Delete(Path.Combine(sdkPackagePath, "Jdk"), true);
                }
                using (var client = new HttpClient())
                {
                    var stream = await client.GetStreamAsync("https://download.visualstudio.microsoft.com/download/pr/d6ef5c3d-5895-4f22-84ac-1f13568b5389/25f38322a6bf3b8116b75e0a303cf492/microsoft-jdk-17.0.7-windows-x64.zip");
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
                    Directory.Delete(Path.Combine(sdkPackagePath, "Sdk"), true);
                }
                using (var client = new HttpClient())
                {
                    var stream = await client.GetStreamAsync("https://dl.google.com/android/repository/commandlinetools-win-9477386_latest.zip");
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
                        { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", "jdk-17.0.7+7") },
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
                                { "JAVA_HOME", Path.Combine(sdkPackagePath, "Jdk", "jdk-17.0.7+7") },
                            }
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken);
                }
            }
            File.WriteAllText(Path.Combine(sdkPackagePath, "ndk-version.txt"), versions.ndk);
            File.WriteAllText(Path.Combine(sdkPackagePath, "jre-version.txt"), "jdk-17.0.7+7");
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
