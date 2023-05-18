namespace Redpoint.UET.SdkManagement
{
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.CSharp.Syntax;
    using Microsoft.Extensions.Logging;
    using Redpoint.AsyncFileUtilities;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.SdkManagement.WindowsSdk;
    using System;
    using System.Collections.Concurrent;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Reflection;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    [SupportedOSPlatform("windows")]
    public class WindowsSdkSetup : ISdkSetup
    {
        private readonly ILogger<WindowsSdkSetup> _logger;
        private readonly IProcessExecutor _processExecutor;

        public WindowsSdkSetup(
            ILogger<WindowsSdkSetup> logger,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _processExecutor = processExecutor;
        }

        public string PlatformName => "Windows";

        private static ConcurrentDictionary<string, Assembly> _cachedCompiles = new ConcurrentDictionary<string, Assembly>();

        internal static async Task<(
            string windowsSdkPreferredVersion,
            string visualCppMinimumVersion,
            string[] suggestedComponents)> ParseVersions(
            string microsoftPlatformSdkFileContent,
            string versionNumberFileContent,
            string versionNumberRangeFileContent)
        {
            Assembly targetAssembly;
            if (!_cachedCompiles.TryGetValue(microsoftPlatformSdkFileContent, out targetAssembly!))
            {
                var versionNumberSyntaxTree = CSharpSyntaxTree.ParseText(versionNumberFileContent);
                var versionNumberSyntaxTreeRoot = await versionNumberSyntaxTree.GetRootAsync();
                var versionNumberRangeSyntaxTree = CSharpSyntaxTree.ParseText(versionNumberRangeFileContent);
                var versionNumberRangeSyntaxTreeRoot = await versionNumberRangeSyntaxTree.GetRootAsync();

                var syntaxTree = CSharpSyntaxTree.ParseText(microsoftPlatformSdkFileContent);
                var syntaxRoot = await syntaxTree.GetRootAsync();

                // We need all of the static members (that hold the actual versions)
                var staticMembers = syntaxRoot.DescendantNodes()
                    .OfType<FieldDeclarationSyntax>()
                    .Where(x => x.Modifiers.Any(x => x.IsKind(SyntaxKind.StaticKeyword)))
                    .Select(x => x.ToString());
                var staticMemberCode = string.Join("\n", staticMembers);

                var classCode = @$"
#nullable enable
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
class WindowsVersionLoader
{{
    {staticMemberCode}
    {versionNumberSyntaxTreeRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First().ToString()}
    {versionNumberRangeSyntaxTreeRoot.DescendantNodes().OfType<ClassDeclarationSyntax>().First().ToString()}
}}
";
                var newSyntaxTree = CSharpSyntaxTree.ParseText(classCode);
                var newSyntaxTreeCode = newSyntaxTree.ToString();
                var rtPath = Path.GetDirectoryName(typeof(object).Assembly.Location) +
                             Path.DirectorySeparatorChar;
                var compilation = CSharpCompilation.Create("WindowsVersionLoader.cs")
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                    .WithReferences(
                        MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Private.CoreLib.dll")),
                        MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Linq.dll")),
                        MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Runtime.dll")))
                    .AddSyntaxTrees(newSyntaxTree);
                using (var stream = new MemoryStream())
                {
                    var compilationResult = compilation.Emit(stream);
                    if (!compilationResult.Success)
                    {
                        throw new InvalidOperationException(string.Join("\n", compilationResult.Diagnostics.Select(x => x.GetMessage())));
                    }
                    targetAssembly = Assembly.Load(stream.ToArray());
                }
                _cachedCompiles.AddOrUpdate(microsoftPlatformSdkFileContent, targetAssembly, (_, _) => targetAssembly);
            }

            var windowsVersionLoader = targetAssembly.GetType("WindowsVersionLoader")!;

            // Read the PreferredWindowsSdkVersions (which is an array of VersionNumber).
            dynamic preferredWindowsSdkVersionsArray = windowsVersionLoader
                .GetField("PreferredWindowsSdkVersions", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            var preferredWindowsSdkVersion = preferredWindowsSdkVersionsArray[0].ToString();

            // Read the PreferredVisualCppVersions (which is an array of VersionNumberRange).
            dynamic preferredVisualCppVersionsArray = windowsVersionLoader
                .GetField("PreferredVisualCppVersions", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            object visualCppVersion = preferredVisualCppVersionsArray[0];
            dynamic minimumVisualCppVersionObject =
                visualCppVersion.GetType().GetProperty("Min", BindingFlags.Instance | BindingFlags.Public)!
                .GetValue(visualCppVersion)!;
            var minimumVisualCppVersion = minimumVisualCppVersionObject.ToString();

            // Read the suggested components.
            var visualStudioSuggestedComponents = (string[])windowsVersionLoader
                .GetField("VisualStudioSuggestedComponents", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            var visualStudio2022SuggestedComponents = (string[])windowsVersionLoader
                .GetField("VisualStudio2022SuggestedComponents", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;

            return (
                preferredWindowsSdkVersion,
                minimumVisualCppVersion,
                visualStudioSuggestedComponents.Concat(visualStudio2022SuggestedComponents).ToArray());
        }

        private async Task<(
            VersionNumber windowsSdkPreferredVersion,
            VersionNumber visualCppMinimumVersion,
            string[] suggestedComponents)> GetVersions(string unrealEnginePath)
        {
            var microsoftPlatformSdkFileContent = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "Platform",
                "Windows",
                "MicrosoftPlatformSDK.Versions.cs"));
            var versionNumberFileContent = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "System",
                "VersionNumber.cs"));
            var versionNumberRangeFileContent = await File.ReadAllTextAsync(Path.Combine(
                unrealEnginePath,
                "Engine",
                "Source",
                "Programs",
                "UnrealBuildTool",
                "System",
                "VersionNumberRange.cs"));
            var rawVersions = await ParseVersions(
                microsoftPlatformSdkFileContent,
                versionNumberFileContent,
                versionNumberRangeFileContent);
            return (
                VersionNumber.Parse(rawVersions.windowsSdkPreferredVersion),
                VersionNumber.Parse(rawVersions.visualCppMinimumVersion),
                rawVersions.suggestedComponents);
        }

        public async Task<string> ComputeSdkPackageId(string unrealEnginePath, CancellationToken cancellationToken)
        {
            var versions = await GetVersions(unrealEnginePath);
            return $"{versions.windowsSdkPreferredVersion}-{versions.visualCppMinimumVersion}";
        }

        public async Task GenerateSdkPackage(string unrealEnginePath, string sdkPackagePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Retrieving desired versions from Unreal Engine source code...");
            var versions = await GetVersions(unrealEnginePath);

            const string rootManifestUrl = "https://aka.ms/vs/17/release/channel";
            var serializerOptions = new JsonSerializerOptions
            {
                Converters =
                {
                    new VisualStudioManifestPackageDependencyJsonConverter()
                }
            };

            // Load the root manifest.
            _logger.LogInformation("Downloading the root manifest...");
            VisualStudioManifest rootManifest;
            using (var client = new HttpClient())
            {
                rootManifest = (await client.GetFromJsonAsync<VisualStudioManifest>(rootManifestUrl, serializerOptions))!;
            }

            // In the root manifest, locate the manifest with all the packages.
            _logger.LogInformation("Downloading the package manifest...");
            VisualStudioManifest packagesManifest;
            using (var client = new HttpClient())
            {
                var packagesManifestUrl = rootManifest.ChannelItems!.First(x => x.Type == "Manifest");
                packagesManifest = (await client.GetFromJsonAsync<VisualStudioManifest>(packagesManifestUrl.Payloads!.First().Url, serializerOptions))!;
            }

            // Generate a dictionary of components based on their ID.
            _logger.LogInformation("Computing package map...");
            var componentLookup = new Dictionary<string, VisualStudioManifestChannelItem>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var package in packagesManifest.Packages!)
            {
                if (package.MachineArch != null && package.MachineArch != "x64")
                {
                    continue;
                }

                if (package.Language != null && package.Language != "en-US")
                {
                    continue;
                }

                if (!componentLookup.ContainsKey(package.Id!))
                {
                    componentLookup.Add(package.Id!, package);
                }
            }

            // Generate the list of components to install.
            var componentsToInstall = new HashSet<string>
            {
                // We need the setup configuration component, which provides the COM component
                // that Unreal Engine uses to locate Visual Studio installations.
                "Microsoft.VisualStudio.Setup.Configuration"
            };

            // Locate the VC components directly so we can add
            // them to the desired components.
            _logger.LogInformation("Determining MSVC compiler components to install...");
            var vcComponentSuffixes = new[]
            {
                ".tools.hostx64.targetx64.base",
                ".tools.hostx64.targetx64.res.base",
                ".crt.headers.base",
                ".crt.x64.desktop.base",
                ".crt.x64.store.base",
                ".crt.source.base",
                ".asan.headers.base",
                ".asan.x64.base"
            };
            var numericVersionSignifier = new Regex("^[0-9\\.]+$");
            var vcComponentsByVersionSignifier = new Dictionary<string, HashSet<string>>();
            foreach (var vcComponent in packagesManifest.Packages!.Where(x => x.Id!.StartsWith("microsoft.vc.", StringComparison.InvariantCultureIgnoreCase)))
            {
                var id = vcComponent.Id!.ToLowerInvariant();
                foreach (var suffix in vcComponentSuffixes)
                {
                    if (id.EndsWith(suffix))
                    {
                        var versionSignifier = id.Substring("microsoft.vc.".Length);
                        versionSignifier = versionSignifier.Substring(0, versionSignifier.Length - suffix.Length);
                        if (numericVersionSignifier.IsMatch(versionSignifier))
                        {
                            if (VersionNumber.Parse(vcComponent.Version!) >= versions.visualCppMinimumVersion)
                            {
                                if (!vcComponentsByVersionSignifier.ContainsKey(versionSignifier))
                                {
                                    vcComponentsByVersionSignifier[versionSignifier] = new HashSet<string>();
                                }

                                vcComponentsByVersionSignifier[versionSignifier].Add(vcComponent.Id!);
                            }
                        }
                    }
                }
            }

            // Pick the lowest version available (in case a new version of MSVC breaks things and UE hasn't been
            // updated to know about the breakage).
            var lowestVersion = vcComponentsByVersionSignifier.Keys.OrderBy(x => x).First();
            foreach (var component in vcComponentsByVersionSignifier[lowestVersion])
            {
                _logger.LogInformation($"Adding the following component to the install manifest: {component} (from MSVC)");
                componentsToInstall.Add(component);
            }

            // Try to find the Windows SDK.
            var winSdkVersion = $"{versions.windowsSdkPreferredVersion.Major}.{versions.windowsSdkPreferredVersion.Minor}.{versions.windowsSdkPreferredVersion.Patch}";
            foreach (var component in componentLookup.Keys)
            {
                if (component == $"Win10SDK_{winSdkVersion}" ||
                    component == $"Win11SDK_{winSdkVersion}")
                {
                    _logger.LogInformation($"Adding the following component to the install manifest: {component} (from Windows SDK)");
                    componentsToInstall.Add(component);
                }
            }

            // Add all the components recursively that are desired.
            void RecursivelyAddComponent(string componentId, string fromComponentId)
            {
                if (!componentLookup!.ContainsKey(componentId))
                {
                    return;
                }

                if (!componentsToInstall!.Contains(componentId))
                {
                    var component = componentLookup![componentId];

                    if (component.Type != "Workload")
                    {
                        _logger.LogInformation($"Adding the following component to the install manifest: {componentId} (dependency of {fromComponentId})");
                    }
                    componentsToInstall.Add(componentId);

                    if (component.Dependencies != null)
                    {
                        foreach (var dep in component.Dependencies)
                        {
                            // Ignore recommended and optional components.
                            if (dep.Value.Type == "Recommended" ||
                                dep.Value.Type == "Optional")
                            {
                                continue;
                            }

                            // Ignore a component if it requires something other than the Build Tools.
                            if (dep.Value.When != null)
                            {
                                if (!dep.Value.When.Contains("Microsoft.VisualStudio.Product.BuildTools"))
                                {
                                    continue;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(dep.Value.Id))
                            {
                                RecursivelyAddComponent(dep.Value.Id, componentId);
                            }
                            else
                            {
                                RecursivelyAddComponent(dep.Key, componentId);
                            }
                        }
                    }
                }
            };
            foreach (var component in versions.suggestedComponents)
            {
                RecursivelyAddComponent(component, "Unreal Engine");
            }

            // Install all of the components.
            var componentsToInstallArray = componentsToInstall.ToArray();
            for (int i = 0; i < componentsToInstallArray.Length; i++)
            {
                var componentId = componentsToInstallArray[i];
                var component = componentLookup![componentId];

                _logger.LogInformation($"({i + 1}/{componentsToInstallArray.Length}) Processing component for installation: {component.Id}");

                switch (component.Type)
                {
                    case "Vsix":
                        {
                            await ExtractVsixComponent(component, sdkPackagePath, cancellationToken);
                        }
                        break;
                    case "Exe":
                        {
                            await ExtractExecutableComponent(component, sdkPackagePath, cancellationToken);
                        }
                        break;
                    case "Msi":
                        {
                            if (componentId == "Microsoft.VisualStudio.Setup.Configuration")
                            {
                                await ExtractMsiComponent(component, sdkPackagePath, cancellationToken);
                            }
                        }
                        break;
                }
            }

            // Write out the environment file.
            var msvcVersion = Path.GetFileName(Directory.GetDirectories(Path.Combine(sdkPackagePath, "VS2022", "VC", "Tools", "MSVC"))[0]);
            var msvcRedistVersion = Path.GetFileName(Directory.GetDirectories(Path.Combine(sdkPackagePath, "VS2022", "VC", "Redist", "MSVC"))[0]);
            var winsdkVersion = Path.GetFileName(Directory.GetDirectories(Path.Combine(sdkPackagePath, "Windows Kits", "10", "bin"))[0]);
            var envs = new Dictionary<string, string>
            {
                { "UES_VS_INSTANCE_ID", $"{versions.windowsSdkPreferredVersion}-{versions.visualCppMinimumVersion}" },
                { "VCToolsInstallDir", $"{Path.Combine("<root>", "VS2022", "VC", "Tools", "MSVC", msvcVersion)}\\" },
                { "VCToolsVersion", msvcVersion },
                { "VisualStudioVersion", "17.0" },
                { "VCINSTALLDIR", $"{Path.Combine("<root>", "VS2022", "VC")}\\" },
                { "DevEnvDir", $"{Path.Combine("<root>", "VS2022", "Common7", "IDE")}\\" },
                { "VCIDEInstallDir", $"{Path.Combine("<root>", "VS2022", "Common7", "IDE", "VC")}\\" },
                { "VCToolsRedistDir", $"{Path.Combine("<root>", "VS2022", "VC", "Redist", "MSVC", msvcRedistVersion)}\\" },
                { "VS170COMNTOOLS", $"{Path.Combine("<root>", "VS2022", "Common7", "Tools")}\\" },
                { "VSINSTALLDIR", $"{Path.Combine("<root>")}\\" },
                { "WindowsSdkBinPath", $"{Path.Combine("<root>", "Windows Kits", "10", "bin")}\\" },
                { "WindowsSdkDir", $"{Path.Combine("<root>", "Windows Kits", "10")}\\" },
                { "WindowsSDKLibVersion", $"{winsdkVersion}\\" },
                { "WindowsSdkVerBinPath", $"{Path.Combine("<root>", "Windows Kits", "10", "bin", winsdkVersion)}\\" },
                { "WindowsSDKVersion", $"{winsdkVersion}\\" },
                {
                    "+PATH",
                    string.Join(
                        ";",
                        new[]
                        {
                            $"{Path.Combine("<root>", "VS2022", "VC", "Tools", "MSVC", msvcVersion, "bin", "Hostx64", "x64")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "bin", winsdkVersion, "x64")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "bin", winsdkVersion, "x64", "ucrt")}"
                        }
                    )
                },
                {
                    "INCLUDE",
                    string.Join(
                        ";",
                        new[]
                        {
                            $"{Path.Combine("<root>", "VS2022", "VC", "Tools", "MSVC", msvcVersion, "include")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Include", winsdkVersion, "ucrt")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Include", winsdkVersion, "shared")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Include", winsdkVersion, "um")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Include", winsdkVersion, "winrt")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Include", winsdkVersion, "cppwinrt")}"
                        }
                    )
                },
                {
                    "LIB",
                    string.Join(
                        ";",
                        new[]
                        {
                            $"{Path.Combine("<root>", "VS2022", "VC", "Tools", "MSVC", msvcVersion, "lib", "x64")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Lib", winsdkVersion, "ucrt", "x64")}",
                            $"{Path.Combine("<root>", "Windows Kits", "10", "Lib", winsdkVersion, "um", "x64")}"
                        }
                    )
                },
            };
            File.WriteAllText(Path.Combine(sdkPackagePath, "envs.json"), JsonSerializer.Serialize(envs, new JsonSerializerOptions { WriteIndented = true }));
            var batchLines = new List<string>();
            foreach (var kv in envs)
            {
                var key = kv.Key;
                var append = false;
                if (kv.Key.StartsWith("+"))
                {
                    key = kv.Key.Substring(1);
                    append = true;
                }
                if (append)
                {
                    batchLines.Add($"@set {key}={kv.Value.Replace("<root>", "%~dp0")};%{key}%");
                }
                else
                {
                    batchLines.Add($"@set {key}={kv.Value.Replace("<root>", "%~dp0")}");
                }
            }
            batchLines.Add("cmd");
            File.WriteAllText(Path.Combine(sdkPackagePath, "StartShell.bat"), string.Join("\r\n", batchLines));
        }

        private async Task ExtractVsixComponent(VisualStudioManifestChannelItem component, string sdkPackagePath, CancellationToken cancellationToken)
        {
            var vs2022 = Path.Combine(sdkPackagePath, "VS2022");
            Directory.CreateDirectory(vs2022);
            foreach (var payload in component.Payloads!)
            {
                var filename = Path.GetFileName(payload.FileName!);
                _logger.LogInformation($"Downloading and extracting VSIX: {filename} ({payload.Size / 1024 / 1024} MB)");
                using (var client = new HttpClient())
                {
                    var stream = await client.GetStreamAsync(payload.Url!, cancellationToken);
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (entry.FullName.EndsWith('\\') || entry.FullName.EndsWith('/'))
                            {
                                continue;
                            }

                            var relativeName = HttpUtility.UrlDecode(entry.FullName.Replace("+", "%2B"));
                            if (relativeName.StartsWith("Contents"))
                            {
                                relativeName = relativeName.Substring("Contents".Length + 1);
                            }
                            else
                            {
                                continue;
                            }

                            if (relativeName.Contains('\\') || relativeName.Contains('/'))
                            {
                                var directoryName = Path.GetDirectoryName(relativeName);
                                Directory.CreateDirectory(Path.Combine(vs2022, directoryName!));
                            }

                            entry.ExtractToFile(Path.Combine(vs2022, relativeName), true);
                        }
                    }
                }
            }
        }

        private async Task ExtractExecutableComponent(VisualStudioManifestChannelItem component, string sdkPackagePath, CancellationToken cancellationToken)
        {
            // Inspect the MSI packages in the Windows SDK component.
            _logger.LogInformation("Downloading MSI files...");
            var msiPackagesToAllow = new HashSet<string>
            {
                "Installers\\Windows SDK for Windows Store Apps Tools-x86_en-us.msi",
                "Installers\\Windows SDK for Windows Store Apps Headers-x86_en-us.msi",
                "Installers\\Windows SDK Desktop Headers x86-x86_en-us.msi",
                "Installers\\Windows SDK for Windows Store Apps Libs-x86_en-us.msi",
                "Installers\\Windows SDK Desktop Libs x64-x86_en-us.msi",
                "Installers\\Universal CRT Headers Libraries and Sources-x86_en-us.msi"
            };
            string[] GetCabFilenamesFromMsi(Stream msiData)
            {
                var cabNames = new List<string>();
                while (msiData.Position < msiData.Length)
                {
                    if (msiData.ReadByte() == '.')
                    {
                        if (msiData.ReadByte() == 'c')
                        {
                            if (msiData.ReadByte() == 'a')
                            {
                                if (msiData.ReadByte() == 'b')
                                {
                                    var cabNameLength = 32 + 4;
                                    msiData.Seek(-cabNameLength, SeekOrigin.Current);
                                    var cabNameBytes = new byte[cabNameLength];
                                    msiData.ReadExactly(cabNameBytes);
                                    cabNames.Add(Encoding.ASCII.GetString(cabNameBytes));
                                }
                            }
                        }
                    }
                }
                return cabNames.ToArray();
            }
            var allCabNames = new List<string>();
            var msiNames = new List<string>();
            foreach (var payload in component.Payloads!)
            {
                if (msiPackagesToAllow.Contains(payload.FileName!))
                {
                    var filename = Path.GetFileName(payload.FileName!);
                    msiNames.Add(filename);
                    _logger.LogInformation($"Downloading and parsing MSI: {filename} ({payload.Size / 1024 / 1024} MB)");
                    using (var client = new HttpClient())
                    {
                        var targetPath = Path.Combine(sdkPackagePath, "__Installers", filename);
                        Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                        using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            var stream = await client.GetStreamAsync(payload.Url!, cancellationToken);
                            await stream.CopyToAsync(file, cancellationToken);
                        }
                        using (var file = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            allCabNames.AddRange(GetCabFilenamesFromMsi(file));
                        }
                    }
                }
            }
            if (allCabNames.Count == 0)
            {
                return;
            }
            _logger.LogInformation($"{allCabNames.Count} CAB files to download and extract...");
            var cabFileToUrl = component.Payloads
                .Where(x => x.FileName!.EndsWith(".cab"))
                .ToDictionary(k => k.FileName!, v => (url: v.Url!, size: v.Size));
            foreach (var cabName in allCabNames)
            {
                var uri = cabFileToUrl[$"Installers\\{cabName}"];
                using (var client = new HttpClient())
                {
                    var targetPath = Path.Combine(sdkPackagePath, "__Installers", cabName);
                    Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                    using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        _logger.LogInformation($"Downloading CAB: {cabName} ({uri.size / 1024 / 1024} MB)");
                        var stream = await client.GetStreamAsync(uri.url, cancellationToken);
                        await stream.CopyToAsync(file, cancellationToken);
                    }
                }
            }
            _logger.LogInformation($"Executing MSI files to extract Windows SDK...");
            var windowsKitsPath = Path.Combine(sdkPackagePath);
            foreach (var msiFile in msiNames)
            {
                _logger.LogInformation($"Extracting MSI: {msiFile}");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                        Arguments = new[]
                        {
                            "/a",
                            msiFile,
                            "/quiet",
                            "/qn",
                            $"TARGETDIR={windowsKitsPath}",
                        },
                        WorkingDirectory = Path.Combine(sdkPackagePath, "__Installers")
                    }, CaptureSpecification.Passthrough, cancellationToken);
                if (!File.Exists(Path.Combine(windowsKitsPath, msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFile}");
                }
                File.Delete(Path.Combine(windowsKitsPath, msiFile));
            }
            // Clean up the installers folder that we no longer need.
            await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "__Installers"), true);
        }

        private async Task ExtractMsiComponent(VisualStudioManifestChannelItem component, string sdkPackagePath, CancellationToken cancellationToken)
        {
            // Inspect the MSI packages in the Windows SDK component.
            _logger.LogInformation("Downloading MSI files...");
            var msiNames = new List<string>();
            foreach (var payload in component.Payloads!)
            {
                var filename = Path.GetFileName(payload.FileName!);
                msiNames.Add(filename);
                _logger.LogInformation($"Downloading MSI: {filename} ({payload.Size / 1024 / 1024} MB)");
                using (var client = new HttpClient())
                {
                    var targetPath = Path.Combine(sdkPackagePath, "__Installers", filename);
                    Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                    using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                    {
                        var stream = await client.GetStreamAsync(payload.Url!, cancellationToken);
                        await stream.CopyToAsync(file, cancellationToken);
                    }
                }
            }
            _logger.LogInformation($"Executing MSI files to extract them...");
            var setupPath = Path.Combine(sdkPackagePath, "Setup");
            Directory.CreateDirectory(setupPath);
            foreach (var msiFile in msiNames)
            {
                _logger.LogInformation($"Extracting MSI: {msiFile}");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                        Arguments = new[]
                        {
                            "/a",
                            msiFile,
                            "/quiet",
                            "/qn",
                            $"TARGETDIR={setupPath}",
                        },
                        WorkingDirectory = Path.Combine(sdkPackagePath, "__Installers")
                    }, CaptureSpecification.Passthrough, cancellationToken);
                if (!File.Exists(Path.Combine(setupPath, msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFile}");
                }
                File.Delete(Path.Combine(setupPath, msiFile));
            }
            // Clean up the installers folder that we no longer need.
            await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "__Installers"), true);
        }

        public Task<EnvironmentForSdkUsage> EnsureSdkPackage(string sdkPackagePath, CancellationToken cancellationToken)
        {
            var rawEnvs = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(sdkPackagePath, "envs.json")))!;
            var newEnvs = new Dictionary<string, string>();
            foreach (var kv in rawEnvs)
            {
                if (kv.Key == "UES_VS_INSTANCE_ID")
                {
                    continue;
                }

                newEnvs.Add(kv.Key, kv.Value.Replace("<root>", sdkPackagePath));
            }

            var sdkPackageId = rawEnvs["UES_VS_INSTANCE_ID"];

            var visualStudioInstanceState = $@"
{{
    ""installationName"": ""VisualStudio/UES-{sdkPackageId}"",
    ""installationPath"": ""{sdkPackagePath.Replace("\\", "\\\\")}"",
    ""launchParams"": {{
        ""fileName"": ""Common7\\IDE\\devenv.exe""
    }},
    ""installationVersion"": ""17.9999.0.0"",
    ""channelId"": ""VisualStudio.17.Release"",
    ""installedChannelId"": ""VisualStudio.17.Release"",
    ""channelUri"": ""https://aka.ms/vs/17/release/channel"",
    ""installedChannelUri"": ""https://aka.ms/vs/17/release/channel"",
    ""catalogInfo"": {{
        ""id"": ""VisualStudio/17.5.5+33627.172"",
        ""buildBranch"": ""d17.5"",
        ""buildVersion"": ""17.5.33627.172"",
        ""localBuild"": ""build-lab"",
        ""manifestName"": ""VisualStudio"",
        ""manifestType"": ""installer"",
        ""productDisplayVersion"": ""17.5.5"",
        ""productLine"": ""Dev17"",
        ""productLineVersion"": ""2022"",
        ""productMilestone"": ""RTW"",
        ""productMilestoneIsPreRelease"": ""False"",
        ""productName"": ""Visual Studio"",
        ""productPatchVersion"": ""5"",
        ""productPreReleaseMilestoneSuffix"": ""1.0"",
        ""productSemanticVersion"": ""17.5.5+33627.172"",
        ""requiredEngineVersion"": ""3.5.2150.18781""
    }},
    ""seed"": {{
        ""languages"": [
            ""en-US""
        ]
    }},
    ""localizedResources"": [
        {{
            ""language"": ""en-us"",
            ""title"": ""Visual Studio Build Tools 2022"",
            ""description"": ""The Visual Studio Build Tools allows you to build native and managed MSBuild-based applications without requiring the Visual Studio IDE. There are options to install the Visual C++ compilers and libraries, MFC, ATL, and C++/CLI support."",
            ""license"": ""https://go.microsoft.com/fwlink/?LinkId=2179911""
        }}
    ],
    ""channelResources"": [],
    ""product"": {{
        ""id"": ""Microsoft.VisualStudio.Product.BuildTools"",
        ""version"": ""17.9999.0.0"",
        ""chip"": ""x64"",
        ""type"": ""Product"",
        ""productArch"": ""x64"",
        ""installed"": true,
        ""supportsExtensions"": true
    }},
    ""selectedPackages"": []
}}
";

            // @note
            // For UE's build tooling to find Visual Studio, it uses the ISetupConfiguration2 COM
            // component which is registered to HKCR:\WOW6432Node\CLSID\{177F0C4A-1CD3-4DE7-A32C-71DBBB9FA36D}\InprocServer32
            // and points to C:\ProgramData\Microsoft\VisualStudio\Setup\x86\Microsoft.VisualStudio.Setup.Configuration.Native.dll.
            // OleView can be used to inspect some of these interfaces, but you can also use ILSpy on
            // Microsoft.VisualStudio.Setup.Configuration.Interop.dll to see the layout of interfaces expected.
            // There are broadly two ways we could intercept this stuff to be per-process:
            // - Use Detours to intercept the read of HKLM\SOFTWARE\Microsoft\VisualStudio\Setup\CachePath, which is the
            //   registry key that contains the PATH "C:\ProgramData\Microsoft\VisualStudio\Packages". We'd still need to ship
            //   and register the original COM component inside containers (though maybe this EnsureSdkPackage could register
            //   it on-demand if needed).
            // - Implement our own version of the interfaces i.e. ISetupConfiguration2 and replace the COM component. This doesn't
            //   really work as a strategy for developer machines though, because we wouldn't want to modify the native
            //   component there.
            // I suspect the Detours method is the most viable, and rather trivial. We'd make a Detours DLL that just:
            // - Ensures that child processes get the Detours DLL loaded into them, and
            // - Whenever RegOpenKey and RegQueryValue happen, it checks the local environment variables for something like
            //   DetoursOverride_HKLM_SOFTWARE_Microsoft_Setup_CachePath="..." based on the key being read and returns that
            //   value instead of the original registry value.
            // If we do that, then overriding the instances cache is as simple as setting an environment variable.

            // @note: The portable SDK contains the Microsoft.VisualStudio.Setup.Configuration.dll COM component at
            // <root>\ProgramData\Microsoft\VisualStudio\Setup\x86\Microsoft.VisualStudio.Setup.Configuration.Native.dll

            // @note: It looks like UE can also find MSVC and Windows Kits via UE_SDKS_ROOT, which requires the portable
            // SDK to be mounted as "Win64" underneath a folder, with that parent folder path set in UE_SDKS_ROOT. UBT will
            // still prefer the system-installed toolchains over AutoSDK though, which is less than ideal.

            // @note: This does require Administrator access, but CI builds are already expected to be
            // running with elevated permission.
            Directory.CreateDirectory("C:\\ProgramData\\Microsoft\\VisualStudio\\Packages\\_Instances\\00000001");
            File.WriteAllText("C:\\ProgramData\\Microsoft\\VisualStudio\\Packages\\_Instances\\00000001\\state.json", visualStudioInstanceState);

            return Task.FromResult(new EnvironmentForSdkUsage
            {
                EnvironmentVariables = newEnvs,
            });
        }
    }
}
