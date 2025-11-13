namespace Redpoint.Uet.SdkManagement.AutoSdk.WindowsSdk
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO.Compression;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Web;

    [SupportedOSPlatform("windows")]
    public class WindowsSdkInstaller
    {
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILogger<WindowsSdkInstaller> _logger;

        public WindowsSdkInstaller(
            ISimpleDownloadProgress simpleDownloadProgress,
            IProcessExecutor processExecutor,
            ILogger<WindowsSdkInstaller> logger)
        {
            _simpleDownloadProgress = simpleDownloadProgress;
            _processExecutor = processExecutor;
            _logger = logger;
        }

        internal async Task InstallSdkToPath(WindowsSdkInstallerTarget versions, string sdkPackagePath, CancellationToken cancellationToken)
        {
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
                rootManifest = (await client.GetFromJsonAsync(rootManifestUrl, new VisualStudioJsonSerializerContext(new JsonSerializerOptions(serializerOptions)).VisualStudioManifest, cancellationToken: cancellationToken).ConfigureAwait(false))!;
            }

            // In the root manifest, locate the manifest with all the packages.
            _logger.LogInformation("Downloading the package manifest...");
            VisualStudioManifest packagesManifest;
            using (var client = new HttpClient())
            {
                var packagesManifestUrl = rootManifest.ChannelItems!.First(x => x.Type == "Manifest");
                packagesManifest = (await client.GetFromJsonAsync(packagesManifestUrl.Payloads!.First().Url, new VisualStudioJsonSerializerContext(new JsonSerializerOptions(serializerOptions)).VisualStudioManifest, cancellationToken: cancellationToken).ConfigureAwait(false))!;
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

                componentLookup.TryAdd(package.Id!, package);
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
                ".crt.x86.desktop.base",
                ".crt.x64.desktop.base",
                ".crt.x86.onecore.desktop.base",
                ".crt.x64.onecore.desktop.base",
                ".crt.x86.store.base",
                ".crt.x64.store.base",
                ".crt.source.base",
                ".asan.headers.base",
                ".asan.x86.base",
                ".asan.x64.base"
            };
            var numericVersionSignifier = new Regex("^[0-9\\.]+$");
            var vcComponentsByVersionSignifier = new Dictionary<string, HashSet<string>>();
            foreach (var vcComponent in packagesManifest.Packages!.Where(x => x.Id!.StartsWith("microsoft.vc.", StringComparison.InvariantCultureIgnoreCase)))
            {
                var id = vcComponent.Id!.ToLowerInvariant();
                foreach (var suffix in vcComponentSuffixes)
                {
                    if (id.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        var versionSignifier = id["microsoft.vc.".Length..];
                        versionSignifier = versionSignifier[..^suffix.Length];
                        if (numericVersionSignifier.IsMatch(versionSignifier))
                        {
                            if (VersionNumber.Parse(vcComponent.Version!) >= versions.VisualCppMinimumVersion)
                            {
                                if (!vcComponentsByVersionSignifier.TryGetValue(versionSignifier, out HashSet<string>? versionIds))
                                {
                                    versionIds = new HashSet<string>();
                                    vcComponentsByVersionSignifier[versionSignifier] = versionIds;
                                }

                                versionIds.Add(vcComponent.Id!);
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
            var winSdkVersion = $"{versions.WindowsSdkPreferredVersion.Major}.{versions.WindowsSdkPreferredVersion.Minor}.{versions.WindowsSdkPreferredVersion.Patch}";
            var foundWinSdkVersion = false;
            foreach (var component in componentLookup.Keys)
            {
                if (component == $"Win10SDK_{winSdkVersion}" ||
                    component == $"Win11SDK_{winSdkVersion}")
                {
                    _logger.LogInformation($"Adding the following component to the install manifest: {component} (from Windows SDK)");
                    componentsToInstall.Add(component);
                    foundWinSdkVersion = false;
                }
            }

            // If we can't find the Windows SDK via the Visual Studio manifest, fallback to known ISO files.
            var isoFallbackUrl = string.Empty;
            if (!foundWinSdkVersion)
            {
                if (winSdkVersion == "10.0.18362")
                {
                    isoFallbackUrl = "https://go.microsoft.com/fwlink/?linkid=2083448";
                }
                else if (winSdkVersion == "10.0.19041")
                {
                    isoFallbackUrl = "https://go.microsoft.com/fwlink/?linkid=2312004";
                }
                else if (winSdkVersion == "10.0.22621")
                {
                    isoFallbackUrl = "https://go.microsoft.com/fwlink/?linkid=2312900";
                }
                else
                {
                    _logger.LogError($"Unable to find Visual Studio manifest for Windows SDK {winSdkVersion}. A fallback ISO URL likely needs to be added to UET.");
                }
            }

            // Add all the components recursively that are desired.
            void RecursivelyAddComponent(string componentId, string fromComponentId)
            {
                if (!componentLookup!.TryGetValue(componentId, out VisualStudioManifestChannelItem? component))
                {
                    return;
                }

                if (!componentsToInstall!.Contains(componentId))
                {
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
            }
            ;
            foreach (var component in versions.SuggestedComponents)
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
                            await ExtractVsixComponent(component, sdkPackagePath, cancellationToken).ConfigureAwait(false);
                        }
                        break;
                    case "Exe":
                        {
                            await ExtractExecutableComponent(component, sdkPackagePath, cancellationToken).ConfigureAwait(false);
                        }
                        break;
                    case "Msi":
                        {
                            if (componentId == "Microsoft.VisualStudio.Setup.Configuration")
                            {
                                await ExtractMsiComponent(component, sdkPackagePath, cancellationToken).ConfigureAwait(false);
                            }
                        }
                        break;
                }
            }

            // If there is a fallback ISO, extract it and then install the Windows SDK from it.
            if (!string.IsNullOrWhiteSpace(isoFallbackUrl))
            {
                using (var client = new HttpClient())
                {
                    _logger.LogInformation($"Downloading fallback ISO: {isoFallbackUrl}");
                    var cache = Path.Combine(sdkPackagePath, "Cache");
                    Directory.CreateDirectory(cache);
                    using (var fileStream = new FileStream(Path.Combine(cache, "winsdk.iso"), FileMode.Create, FileAccess.ReadWrite))
                    {
                        await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                            client,
                            new Uri(isoFallbackUrl),
                            stream => stream.CopyToAsync(fileStream, cancellationToken),
                            cancellationToken).ConfigureAwait(false);
                        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }

                    _logger.LogInformation($"Mounting ISO image...");
                    var driveLetterStringBuilder = new StringBuilder();
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                            Arguments =
                            [
                                "-ExecutionPolicy",
                                "Bypass",
                                "-Command",
                                $@"(Mount-DiskImage -ImagePath ""{Path.Combine(cache, "winsdk.iso")}"" | Get-Volume).DriveLetter"
                            ]
                        },
                        CaptureSpecification.CreateFromStdoutStringBuilder(driveLetterStringBuilder),
                        cancellationToken);
                    try
                    {
                        _logger.LogInformation($"ISO image mounted on drive {driveLetterStringBuilder}.");
                        await ExtractExecutableComponentFromIso(driveLetterStringBuilder.ToString().Trim(), sdkPackagePath, cancellationToken);
                    }
                    finally
                    {
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe",
                                Arguments =
                                [
                                    "-ExecutionPolicy",
                                    "Bypass",
                                    "-Command",
                                    $@"Dismount-DiskImage -ImagePath ""{Path.Combine(cache, "winsdk.iso")}"""
                                ]
                            },
                            CaptureSpecification.Passthrough,
                            cancellationToken);
                    }
                }
            }

            // Download "Clang for Unreal Engine" and install it into the LLVM directory for AutoSDK.
            try
            {
                using (var client = new HttpClient())
                {
                    _logger.LogInformation($"Retrieving metadata for Clang for Unreal Engine...");
                    var info = await client.GetStringAsync(
                        new Uri("https://github.com/RedpointGames/llvm-project/releases/latest/download/info"),
                        cancellationToken);
                    var name = info
                        .Replace("\r\n", "\n", StringComparison.Ordinal)
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(x =>
                        {
                            var components = x.Split('=', 2);
                            if (components.Length != 2)
                            {
                                return (platform: string.Empty, name: string.Empty);
                            }
                            return (platform: components[0], name: components[1]);
                        })
                        .FirstOrDefault(x => x.platform == "win64")
                        .name;
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        _logger.LogInformation($"Downloading and extracting Clang for Unreal Engine: {name}");
                        var cache = Path.Combine(sdkPackagePath, "Cache");
                        Directory.CreateDirectory(cache);
                        var llvm = Path.Combine(sdkPackagePath, "LLVM");
                        Directory.CreateDirectory(llvm);
                        using (var fileStream = new FileStream(Path.Combine(cache, name), FileMode.Create, FileAccess.ReadWrite))
                        {
                            // Download to a file so that the data is seekable for ZipArchive. This is necessary for
                            // large downloads where ZipArchive would otherwise try to copy the entire stream into
                            // memory with a MemoryStream and then fail due to the data being too big for MemoryStream.
                            await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                client,
                                new Uri($"https://github.com/RedpointGames/llvm-project/releases/latest/download/{name}"),
                                stream => stream.CopyToAsync(fileStream, cancellationToken),
                                cancellationToken).ConfigureAwait(false);
                            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                            fileStream.Seek(0, SeekOrigin.Begin);
                            using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                            {
                                foreach (var entry in archive.Entries)
                                {
                                    if (entry.FullName.EndsWith('\\') || entry.FullName.EndsWith('/'))
                                    {
                                        continue;
                                    }

                                    var baseName = Path.GetFileNameWithoutExtension(name);
                                    var relativeName = HttpUtility.UrlDecode(entry.FullName.Replace("+", "%2B", StringComparison.Ordinal));
                                    if (relativeName.StartsWith(baseName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        relativeName = relativeName[(baseName.Length + 1)..];
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    if (relativeName.Contains('\\', StringComparison.Ordinal) || relativeName.Contains('/', StringComparison.Ordinal))
                                    {
                                        var directoryName = Path.GetDirectoryName(relativeName);
                                        Directory.CreateDirectory(Path.Combine(llvm, directoryName!));
                                    }

                                    entry.ExtractToFile(Path.Combine(llvm, relativeName), true);
                                }
                            }
                        }

                        _logger.LogInformation($"Creating symbolic link for Clang for Unreal Engine LLVM as VS2022 LLVM...");
                        var llvmVs = Path.Combine(sdkPackagePath, "VS2022", "VC", "Tools", "Llvm");
                        await DirectoryAsync.DeleteAsync(llvmVs, true);
                        Directory.CreateSymbolicLink(
                            llvmVs,
                            @"..\..\..\LLVM");
                        Directory.CreateDirectory(Path.Combine(llvmVs, "x64"));
                        await DirectoryAsync.CopyAsync(
                            Path.Combine(llvmVs, "bin"),
                            Path.Combine(llvmVs, "x64", "bin"),
                            true);
                    }
                    else
                    {
                        _logger.LogWarning($"Missing 'info' asset on GitHub download for Clang for Unreal Engine; can't download and install.");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to install Clang for Unreal Engine: {ex}");
            }

            // Write out the environment file.
            var msvcVersion = Path.GetFileName(Directory.GetDirectories(Path.Combine(sdkPackagePath, "VS2022", "VC", "Tools", "MSVC"))[0]);
            var winsdkVersion = Path.GetFileName(Directory.GetDirectories(Path.Combine(sdkPackagePath, "Windows Kits", "10", "bin"))[0]);
            var envs = new Dictionary<string, string>
            {
                { "UES_VS_INSTANCE_ID", $"{versions.WindowsSdkPreferredVersion}-{versions.VisualCppMinimumVersion}" },
                { "VCToolsInstallDir", $"{Path.Combine("<root>", "VS2022", "VC", "Tools", "MSVC", msvcVersion)}\\" },
                { "VCToolsVersion", msvcVersion },
                { "VisualStudioVersion", "17.0" },
                { "VCINSTALLDIR", $"{Path.Combine("<root>", "VS2022", "VC")}\\" },
                { "DevEnvDir", $"{Path.Combine("<root>", "VS2022", "Common7", "IDE")}\\" },
                { "VCIDEInstallDir", $"{Path.Combine("<root>", "VS2022", "Common7", "IDE", "VC")}\\" },
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
            if (Directory.Exists(Path.Combine(sdkPackagePath, "VS2022", "VC", "Redist", "MSVC")))
            {
                var subdirs = Directory.GetDirectories(Path.Combine(sdkPackagePath, "VS2022", "VC", "Redist", "MSVC"));
                if (subdirs.Length > 0)
                {
                    var msvcRedistVersion = Path.GetFileName(subdirs[0]);
                    envs["VCToolsRedistDir"] = $"{Path.Combine("<root>", "VS2022", "VC", "Redist", "MSVC", msvcRedistVersion)}\\";
                }
            }

            File.WriteAllText(Path.Combine(sdkPackagePath, "envs.json"), JsonSerializer.Serialize(envs, new VisualStudioJsonSerializerContext(new JsonSerializerOptions { WriteIndented = true }).DictionaryStringString));
            var batchLines = new List<string>();
            foreach (var kv in envs)
            {
                var key = kv.Key;
                var append = false;
                if (kv.Key.StartsWith('+'))
                {
                    key = kv.Key[1..];
                    append = true;
                }
                if (append)
                {
                    batchLines.Add($"@set {key}={kv.Value.Replace("<root>", "%~dp0", StringComparison.OrdinalIgnoreCase)};%{key}%");
                }
                else
                {
                    batchLines.Add($"@set {key}={kv.Value.Replace("<root>", "%~dp0", StringComparison.OrdinalIgnoreCase)}");
                }
            }
            batchLines.Add("cmd");
            File.WriteAllText(Path.Combine(sdkPackagePath, "StartShell.bat"), string.Join("\r\n", batchLines));

            // For all the directories underneath VS2022/VC/Tools/MSVC, link them to the root, which
            // makes this work with AutoSDK.
            foreach (var directory in new DirectoryInfo(Path.Combine(sdkPackagePath, "VS2022", "VC", "Tools", "MSVC")).GetDirectories())
            {
                var linkLocation = Path.Combine(sdkPackagePath, "VS2022", directory.Name);
                var linkTarget = Path.Combine("VC", "Tools", "MSVC", directory.Name);
                _logger.LogInformation($"Creating symbolic link for Windows AutoSDK '{linkLocation}' to point to: {linkTarget}");
                Directory.CreateSymbolicLink(linkLocation, linkTarget);
            }
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
                    var cache = Path.Combine(sdkPackagePath, "Cache");
                    Directory.CreateDirectory(cache);
                    using (var fileStream = new FileStream(Path.Combine(cache, filename), FileMode.Create, FileAccess.ReadWrite))
                    {
                        // Download to a file so that the data is seekable for ZipArchive. This is necessary for
                        // large downloads where ZipArchive would otherwise try to copy the entire stream into
                        // memory with a MemoryStream and then fail due to the data being too big for MemoryStream.
                        await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                            client,
                            new Uri(payload.Url!),
                            stream => stream.CopyToAsync(fileStream, cancellationToken),
                            cancellationToken).ConfigureAwait(false);
                        await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
                        fileStream.Seek(0, SeekOrigin.Begin);
                        using (var archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                        {
                            foreach (var entry in archive.Entries)
                            {
                                if (entry.FullName.EndsWith('\\') || entry.FullName.EndsWith('/'))
                                {
                                    continue;
                                }

                                var relativeName = HttpUtility.UrlDecode(entry.FullName.Replace("+", "%2B", StringComparison.Ordinal));
                                if (relativeName.StartsWith("Contents", StringComparison.OrdinalIgnoreCase))
                                {
                                    relativeName = relativeName[("Contents".Length + 1)..];
                                }
                                else
                                {
                                    continue;
                                }

                                if (relativeName.Contains('\\', StringComparison.Ordinal) || relativeName.Contains('/', StringComparison.Ordinal))
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
        }

        private readonly HashSet<string> _msiPackagesToAllow =
        [
            "Windows SDK for Windows Store Apps Tools-x86_en-us.msi",
            "Windows SDK for Windows Store Apps Headers-x86_en-us.msi",
            "Windows SDK Desktop Headers x86-x86_en-us.msi",
            "Windows SDK for Windows Store Apps Libs-x86_en-us.msi",
            "Windows SDK Desktop Libs x64-x86_en-us.msi",
            "Universal CRT Headers Libraries and Sources-x86_en-us.msi"
        ];

        private async Task ExtractExecutableComponent(
            VisualStudioManifestChannelItem component,
            string sdkPackagePath,
            CancellationToken cancellationToken)
        {
            // Inspect the MSI packages in the Windows SDK component.
            _logger.LogInformation("Downloading MSI files...");
            var msiPackagesToAllow = new HashSet<string>(_msiPackagesToAllow.Select(x => $"Installers\\{x}"));
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
                            await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                client,
                                new Uri(payload.Url!),
                                async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                                cancellationToken).ConfigureAwait(false);
                        }
                        using (var file = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            allCabNames.AddRange(GetCabFilenamesFromMsi(file));
                        }
                    }
                }

                // Use the winsdksetup.exe file to download the debugging tools components.
                if (payload.FileName == "winsdksetup.exe")
                {
                    _logger.LogInformation($"Downloading Windows SDK Setup: {payload.FileName} ({payload.Size / 1024 / 1024} MB)");
                    var targetPath = Path.Combine(sdkPackagePath, "__Installers", payload.FileName);
                    Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                    using (var client = new HttpClient())
                    {
                        using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                client,
                                new Uri(payload.Url!),
                                async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                                cancellationToken).ConfigureAwait(false);
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
                .Where(x => x.FileName!.EndsWith(".cab", StringComparison.Ordinal))
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
                        await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                            client,
                            new Uri(uri.url),
                            async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                            cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            string windowsSdkDebugVersion = "10.0.22621.3233";
            _logger.LogInformation($"Downloading Debugging Tools for Windows from Windows SDK {windowsSdkDebugVersion}...");
            using (var queryClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            }))
            {
                string sdkRoot;
                var request = new HttpRequestMessage(HttpMethod.Get, $"http://go.microsoft.com/fwlink/?prd=11966&pver=1.0&plcid=0x409&clcid=0x409&ar=Windows10&sar=SDK&o1={windowsSdkDebugVersion}");
                var response = await queryClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Redirect)
                {
                    sdkRoot = response.Headers.Location!.AbsoluteUri;
                }
                else
                {
                    throw new SdkSetupPackageGenerationFailedException($"Expected 302 response code for Debugging Tools download!");
                }

                var debugCabNames = new List<string>();
                var debugMsiNames = new List<string>
                {
                    "SDK Debuggers-x86_en-us.msi",
                    "X86 Debuggers And Tools-x86_en-us.msi",
                    "X64 Debuggers And Tools-x64_en-us.msi",
                };
                foreach (var debugMsiFile in debugMsiNames)
                {
                    using (var client = new HttpClient())
                    {
                        var targetPath = Path.Combine(sdkPackagePath, "__Installers", debugMsiFile);
                        Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                        using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            _logger.LogInformation($"Downloading Debugging Tools MSI: {debugMsiFile}");
                            await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                client,
                                new Uri($"{sdkRoot}/Installers/{debugMsiFile}"),
                                async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                                cancellationToken).ConfigureAwait(false);
                        }
                        using (var file = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            debugCabNames.AddRange(GetCabFilenamesFromMsi(file));
                        }
                    }
                    msiNames.Add(debugMsiFile);
                }

                foreach (var cabName in debugCabNames)
                {
                    using (var client = new HttpClient())
                    {
                        var targetPath = Path.Combine(sdkPackagePath, "__Installers", cabName);
                        if (!File.Exists(targetPath))
                        {
                            Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                            using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                            {
                                _logger.LogInformation($"Downloading Debugging Tools CAB: {cabName}");
                                await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                    client,
                                    new Uri($"{sdkRoot}/Installers/{cabName}"),
                                    async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                                    cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation($"Executing MSI files to extract Windows SDK...");
            var windowsKitsPath = Path.Combine(sdkPackagePath);
            foreach (var msiFile in msiNames)
            {
                if (!File.Exists(Path.Combine(sdkPackagePath, "__Installers", msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"Missing expected MSI file: {Path.Combine(sdkPackagePath, "__Installers", msiFile)}");
                }

                _logger.LogInformation($"Extracting MSI: {msiFile}");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                        Arguments = new LogicalProcessArgument[]
                        {
                            "/a",
                            msiFile,
                            "/quiet",
                            "/qn",
                            $"TARGETDIR={windowsKitsPath}",
                        },
                        WorkingDirectory = Path.Combine(sdkPackagePath, "__Installers")
                    }, CaptureSpecification.Passthrough, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(Path.Combine(windowsKitsPath, msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFile}");
                }
                File.Delete(Path.Combine(windowsKitsPath, msiFile));
            }

            // Clean up the installers folder that we no longer need.
            await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "__Installers"), true).ConfigureAwait(false);
        }

        private async Task ExtractExecutableComponentFromIso(
            string driveLetter,
            string sdkPackagePath,
            CancellationToken cancellationToken)
        {
            // Inspect the MSI packages in the Windows SDK component.
            _logger.LogInformation("Downloading MSI files...");
            var msiPackagesToAllow = new HashSet<string>(_msiPackagesToAllow);
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
            foreach (var installerFile in new DirectoryInfo($"{driveLetter}:\\Installers").GetFiles())
            {
                if (msiPackagesToAllow.Contains(installerFile.Name))
                {
                    var filename = Path.GetFileName(installerFile.FullName);
                    msiNames.Add(filename);
                    _logger.LogInformation($"Parsing MSI: {filename}");
                    {
                        var targetPath = Path.Combine(sdkPackagePath, "__Installers", filename);
                        Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                        File.Copy(installerFile.FullName, targetPath);
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
            var cabFileToUrl = new DirectoryInfo($"{driveLetter}:\\Installers").EnumerateFiles("*.cab");
            foreach (var cabName in allCabNames)
            {
                var targetPath = Path.Combine(sdkPackagePath, "__Installers", cabName);
                Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                File.Copy($"{driveLetter}:\\Installers\\{cabName}", targetPath);
            }

            string windowsSdkDebugVersion = "10.0.22621.3233";
            _logger.LogInformation($"Downloading Debugging Tools for Windows from Windows SDK {windowsSdkDebugVersion}...");
            using (var queryClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
            }))
            {
                string sdkRoot;
                var request = new HttpRequestMessage(HttpMethod.Get, $"http://go.microsoft.com/fwlink/?prd=11966&pver=1.0&plcid=0x409&clcid=0x409&ar=Windows10&sar=SDK&o1={windowsSdkDebugVersion}");
                var response = await queryClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == System.Net.HttpStatusCode.Redirect)
                {
                    sdkRoot = response.Headers.Location!.AbsoluteUri;
                }
                else
                {
                    throw new SdkSetupPackageGenerationFailedException($"Expected 302 response code for Debugging Tools download!");
                }

                var debugCabNames = new List<string>();
                var debugMsiNames = new List<string>
                {
                    "SDK Debuggers-x86_en-us.msi",
                    "X86 Debuggers And Tools-x86_en-us.msi",
                    "X64 Debuggers And Tools-x64_en-us.msi",
                };
                foreach (var debugMsiFile in debugMsiNames)
                {
                    using (var client = new HttpClient())
                    {
                        var targetPath = Path.Combine(sdkPackagePath, "__Installers", debugMsiFile);
                        Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                        using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                        {
                            _logger.LogInformation($"Downloading Debugging Tools MSI: {debugMsiFile}");
                            await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                client,
                                new Uri($"{sdkRoot}/Installers/{debugMsiFile}"),
                                async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                                cancellationToken).ConfigureAwait(false);
                        }
                        using (var file = new FileStream(targetPath, FileMode.Open, FileAccess.Read))
                        {
                            debugCabNames.AddRange(GetCabFilenamesFromMsi(file));
                        }
                    }
                    msiNames.Add(debugMsiFile);
                }

                foreach (var cabName in debugCabNames)
                {
                    using (var client = new HttpClient())
                    {
                        var targetPath = Path.Combine(sdkPackagePath, "__Installers", cabName);
                        if (!File.Exists(targetPath))
                        {
                            Directory.CreateDirectory(Path.Combine(sdkPackagePath, "__Installers"));
                            using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                            {
                                _logger.LogInformation($"Downloading Debugging Tools CAB: {cabName}");
                                await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                    client,
                                    new Uri($"{sdkRoot}/Installers/{cabName}"),
                                    async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                                    cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }

            _logger.LogInformation($"Executing MSI files to extract Windows SDK...");
            var windowsKitsPath = Path.Combine(sdkPackagePath);
            foreach (var msiFile in msiNames)
            {
                if (!File.Exists(Path.Combine(sdkPackagePath, "__Installers", msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"Missing expected MSI file: {Path.Combine(sdkPackagePath, "__Installers", msiFile)}");
                }

                _logger.LogInformation($"Extracting MSI: {msiFile}");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                        Arguments = new LogicalProcessArgument[]
                        {
                            "/a",
                            msiFile,
                            "/quiet",
                            "/qn",
                            $"TARGETDIR={windowsKitsPath}",
                        },
                        WorkingDirectory = Path.Combine(sdkPackagePath, "__Installers")
                    }, CaptureSpecification.Passthrough, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(Path.Combine(windowsKitsPath, msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFile}");
                }
                File.Delete(Path.Combine(windowsKitsPath, msiFile));
            }

            // Clean up the installers folder that we no longer need.
            await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "__Installers"), true).ConfigureAwait(false);
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
                        await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                            client,
                            new Uri(payload.Url!),
                            async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                            cancellationToken).ConfigureAwait(false);
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
                        Arguments = new LogicalProcessArgument[]
                        {
                            "/a",
                            msiFile,
                            "/quiet",
                            "/qn",
                            $"TARGETDIR={setupPath}",
                        },
                        WorkingDirectory = Path.Combine(sdkPackagePath, "__Installers")
                    }, CaptureSpecification.Passthrough, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(Path.Combine(setupPath, msiFile)))
                {
                    throw new SdkSetupPackageGenerationFailedException($"MSI extraction failed for: {msiFile}");
                }
                File.Delete(Path.Combine(setupPath, msiFile));
            }
            // Clean up the installers folder that we no longer need.
            await DirectoryAsync.DeleteAsync(Path.Combine(sdkPackagePath, "__Installers"), true).ConfigureAwait(false);
        }
    }
}
