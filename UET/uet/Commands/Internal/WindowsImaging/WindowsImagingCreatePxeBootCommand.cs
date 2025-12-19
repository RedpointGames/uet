namespace UET.Commands.Internal.WindowsImaging
{
    using Microsoft.Extensions.Logging;
    using Redpoint.CommandLine;
    using Redpoint.Concurrency;
    using Redpoint.IO;
    using Redpoint.PackageManagement;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Reflection;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;

    internal class WindowsImagingCreatePxeBootCommand : ICommandDescriptorProvider<UetGlobalCommandContext>
    {
        public static CommandDescriptor<UetGlobalCommandContext> Descriptor => UetCommandDescriptor.NewBuilder()
            .WithOptions<Options>()
            .WithInstance<WindowsImagingCreatePxeBootCommandInstance>()
            .WithCommand(
                builder =>
                {
                    return new Command("create-pxe-boot");
                })
            .Build();

        public sealed class Options
        {
            public Option<DirectoryInfo> Path;
            public Option<bool> NoAutoUpgrade;
            public Option<FileInfo> InstallWimPath;
            public Option<string> Edition;

            public Options()
            {
                Path = new Option<DirectoryInfo>("--path") { IsRequired = true };
                Path.AddAlias("-p");
                NoAutoUpgrade = new Option<bool>("--no-auto-upgrade");
                NoAutoUpgrade.AddAlias("-n");
                InstallWimPath = new Option<FileInfo>("--install-wim") { IsRequired = true };
                InstallWimPath.AddAlias("-i");
                Edition = new Option<string>("--edition", () => "Windows 11 Pro");
                Edition.AddAlias("-e");
            }
        }

        private sealed class WindowsImagingCreatePxeBootCommandInstance : ICommandInstance
        {
            private readonly ILogger<WindowsImagingCreatePxeBootCommandInstance> _logger;
            private readonly IReservationManagerForUet _reservationManagerForUet;
            private readonly IPackageManager _packageManager;
            private readonly IProcessExecutor _processExecutor;
            private readonly ISimpleDownloadProgress _simpleDownloadProgress;
            private readonly Options _options;

            public WindowsImagingCreatePxeBootCommandInstance(
                ILogger<WindowsImagingCreatePxeBootCommandInstance> logger,
                IReservationManagerForUet reservationManagerForUet,
                IPackageManager packageManager,
                IProcessExecutor processExecutor,
                ISimpleDownloadProgress simpleDownloadProgress,
                Options options)
            {
                _logger = logger;
                _reservationManagerForUet = reservationManagerForUet;
                _packageManager = packageManager;
                _processExecutor = processExecutor;
                _simpleDownloadProgress = simpleDownloadProgress;
                _options = options;
            }

            private static async Task<string> GetEmbeddedResourceAsString(string name)
            {
                using (var reader = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream($"UET.Commands.Internal.WindowsImaging.{name}")!))
                {
                    return await reader.ReadToEndAsync();
                }
            }

            public async Task<int> ExecuteAsync(ICommandInvocationContext context)
            {
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var noAutoUpgrade = context.ParseResult.GetValueForOption(_options.NoAutoUpgrade)!;
                var installWimPath = context.ParseResult.GetValueForOption(_options.InstallWimPath)!;
                var edition = context.ParseResult.GetValueForOption(_options.Edition)!;

                Directory.CreateDirectory(path.FullName);

                await _packageManager.InstallOrUpgradePackageToLatestAsync("Microsoft.WindowsADK", context.GetCancellationToken());

                // @note: When accepted to WinGet repository, uncomment this.
                // await _packageManager.InstallOrUpgradePackageToLatestAsync("Microsoft.WindowsADK.WinPEAddon", context.GetCancellationToken());

                _logger.LogInformation("Querying install.wim file for editions...");
                var imageInfo = new StringBuilder();
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"),
                        Arguments = [
                            "/Get-ImageInfo",
                            $"/ImageFile:{installWimPath.FullName}",
                        ],
                    },
                    CaptureSpecification.CreateFromStdoutStringBuilder(imageInfo),
                    context.GetCancellationToken());
                var indexRegex = new Regex("^Index : (?<index>[0-9]+)$");
                var nameRegex = new Regex("^Name : (?<name>.+)$");
                var currentIndex = -1;
                var currentName = string.Empty;
                var foundEdition = false;
                var editions = new List<string>();
                foreach (var line in imageInfo.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
                {
                    var indexMatch = indexRegex.Match(line);
                    var nameMatch = nameRegex.Match(line);

                    if (indexMatch.Success)
                    {
                        _ = int.TryParse(indexMatch.Groups["index"].Value, out currentIndex);
                    }
                    else if (nameMatch.Success)
                    {
                        currentName = nameMatch.Groups["name"].Value;
                    }

                    editions.Add(currentName);

                    if (currentName == edition)
                    {
                        foundEdition = true;
                        break;
                    }
                }
                if (!foundEdition)
                {
                    _logger.LogError($"Provided install.wim file does not contain an entry with the name '{edition}'.");
                    _logger.LogInformation($"The available editions are: {string.Join(", ", editions)}");
                    return 1;
                }
                var installWimIndex = currentIndex;
                _logger.LogInformation($"Edition '{edition}' found at index '{installWimIndex}'.");

                await using ((await _reservationManagerForUet.ReserveAsync("WinPEPreparation").ConfigureAwait(false))
                    .AsAsyncDisposable(out var winPeDestination)
                    .ConfigureAwait(false))
                {
                    var targetPath = Path.Combine(winPeDestination.ReservedPath, "amd64");

                    _logger.LogInformation("Cleaning up existing files...");
                    try
                    {
                        await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"),
                                Arguments = [
                                    "/unmount-image",
                                    $"/mountdir:{Path.Combine(targetPath, "mount")}",
                                    "/discard",
                                ],
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                    }
                    catch
                    {
                    }
                    await DirectoryAsync.DeleteAsync(targetPath, true).ConfigureAwait(false);

                    var adkRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10", "Assessment and Deployment Kit");
                    var winPeRoot = Path.Combine(adkRoot, "Windows Preinstallation Environment");

                    _logger.LogInformation("Copying Windows PE for amd64...");
                    var exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                            Arguments = [
                                "/C",
                                @$"{Path.Combine(adkRoot, "Deployment Tools", "DandISetEnv.bat").Replace(" ", "^ ", StringComparison.Ordinal).Replace("(", "^(", StringComparison.Ordinal).Replace(")", "^)", StringComparison.Ordinal)} & {Path.Combine(winPeRoot, "copype.cmd").Replace(" ", "^ ", StringComparison.Ordinal).Replace("(", "^(", StringComparison.Ordinal).Replace(")", "^)", StringComparison.Ordinal)} amd64 {targetPath.Replace(" ", "^ ", StringComparison.Ordinal).Replace("(", "^(", StringComparison.Ordinal).Replace(")", "^)", StringComparison.Ordinal)}"
                            ],
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        return exitCode;
                    }

                    _logger.LogInformation("Installing optional components into WinPE image...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"),
                            Arguments = [
                                "/mount-image",
                                $"/imagefile:{Path.Combine(targetPath, "media", "sources", "boot.wim")}",
                                $"/mountdir:{Path.Combine(targetPath, "mount")}",
                            ],
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        return exitCode;
                    }
                    try
                    {
                        var cabsToInstall = new[]
                        {
                            "WinPE-WMI",
                            "WinPE-NetFx",
                            "WinPE-Scripting",
                            "WinPE-PowerShell",
                            "WinPE-DismCmdlets",
                            "WinPE-SecureBootCmdlets",
                            "WinPE-StorageWMI",
                            "WinPE-Setup",
                            "WinPE-Setup-Client",
                            "WinPE-SecureStartup",
                        };
                        foreach (var cabToInstall in cabsToInstall)
                        {
                            _logger.LogInformation($"Installing {cabToInstall}...");
                            exitCode = await _processExecutor.ExecuteAsync(
                                new ProcessSpecification
                                {
                                    FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"),
                                    Arguments = [
                                        "/add-package",
                                        $"/image:{Path.Combine(targetPath, "mount")}",
                                        $"/packagepath:{Path.Combine(winPeRoot, "amd64", "WinPE_OCs", $"{cabToInstall}.cab")}",
                                    ],
                                },
                                CaptureSpecification.Passthrough,
                                context.GetCancellationToken());
                            if (exitCode != 0)
                            {
                                return exitCode;
                            }
                        }
                    }
                    finally
                    {
                        exitCode = await _processExecutor.ExecuteAsync(
                            new ProcessSpecification
                            {
                                FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "dism.exe"),
                                Arguments = [
                                    "/unmount-image",
                                    $"/mountdir:{Path.Combine(targetPath, "mount")}",
                                    "/commit",
                                ],
                            },
                            CaptureSpecification.Passthrough,
                            context.GetCancellationToken());
                    }
                    if (exitCode != 0)
                    {
                        return exitCode;
                    }

                    _logger.LogInformation("Downloading files...");
                    var files = new (string url, string filename)[]
                    {
                        ("https://boot.ipxe.org/ipxe.efi", "ipxe.efi"),
                        ("https://github.com/ipxe/wimboot/releases/latest/download/wimboot", "wimboot"),
                        ("https://github.com/RedpointGames/uet/releases/latest/download/uet.exe", "uet.exe"),
                    };
                    using (var client = new HttpClient())
                    {
                        foreach (var file in files)
                        {
                            using (var stream = new FileStream(Path.Combine(path.FullName, file.filename), FileMode.Create))
                            {
                                _logger.LogInformation($"  {file.url}");
                                await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                                    client,
                                    new Uri(file.url),
                                    async download => await download.CopyToAsync(stream, context.GetCancellationToken()),
                                    context.GetCancellationToken());
                            }
                        }
                    }

                    _logger.LogInformation("Creating provisioning package...");
                    var customizationsXml = (await GetEmbeddedResourceAsString("customizations.xml"))
                        .Replace("$$uetpath$$", Path.Combine(path.FullName, "uet.exe"), StringComparison.Ordinal)
                        .Replace("$$installbatchpath$$", Path.Combine(winPeDestination.ReservedPath, "uetinstall.bat"), StringComparison.Ordinal);
                    await File.WriteAllTextAsync(
                        Path.Combine(winPeDestination.ReservedPath, "customizations.xml"),
                        customizationsXml);
                    if (!noAutoUpgrade)
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(winPeDestination.ReservedPath, "uetinstall.bat"),
                            """
                            set LOGFILE=%SystemDrive%\UetInstall.log
                            echo Starting UET install... >> %LOGFILE%
                            echo %~dp0 >> %LOGFILE%
                            .\uet.exe upgrade --wait-for-network --then -- cluster start --auto-upgrade --no-stream-logs --wait-for-sysprep >> %LOGFILE%
                            echo Result: %ERRORLEVEL% >> %LOGFILE%
                            """);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(winPeDestination.ReservedPath, "uetinstall.bat"),
                            """
                            set LOGFILE=%SystemDrive%\UetInstall.log
                            echo Starting UET install... >> %LOGFILE%
                            echo %~dp0 >> %LOGFILE%
                            .\uet.exe cluster start --no-stream-logs --wait-for-sysprep >> %LOGFILE%
                            echo Result: %ERRORLEVEL% >> %LOGFILE%
                            """);
                    }
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = Path.Combine(adkRoot, "Imaging and Configuration Designer", "x86", "icd.exe"),
                            Arguments = [
                                "/Build-ProvisioningPackage",
                                $"/CustomizationXML:\"{Path.Combine(winPeDestination.ReservedPath, "customizations.xml")}\"",
                                $"/PackagePath:\"{Path.Combine(winPeDestination.ReservedPath, "uet.ppkg")}\"",
                                $"/StoreFile:\"{Path.Combine(adkRoot, "Imaging and Configuration Designer", "x86", "Microsoft-Desktop-Provisioning.dat")}\"",
                                "+Overwrite",
                            ]
                        },
                        CaptureSpecification.Passthrough,
                        context.GetCancellationToken());
                    if (exitCode != 0)
                    {
                        return exitCode;
                    }

                    _logger.LogInformation("Extracting enrollment script...");
                    var enrollPs1 = (await GetEmbeddedResourceAsString("enroll.ps1"));
                    await File.WriteAllTextAsync(
                        Path.Combine(winPeDestination.ReservedPath, "enroll.ps1"),
                        enrollPs1);

                    var filesCopy = new (string source, string filename)[]
                    {
                        (Path.Combine(targetPath, "media", "Boot", "BCD"), Path.Combine(path.FullName, "BCD")),
                        (Path.Combine(targetPath, "media", "Boot", "boot.sdi"), Path.Combine(path.FullName, "boot.sdi")),
                        (Path.Combine(targetPath, "media", "sources", "boot.wim"), Path.Combine(path.FullName, "boot.wim")),
                        (Path.Combine(winPeDestination.ReservedPath, "uet.ppkg"), Path.Combine(path.FullName, "uet.ppkg")),
                        (Path.Combine(winPeDestination.ReservedPath, "enroll.ps1"), Path.Combine(path.FullName, "enroll.ps1")),
                        (installWimPath.FullName, Path.Combine(path.FullName, "install.wim")),
                    };
                    foreach (var file in filesCopy)
                    {
                        _logger.LogInformation($"Copying WinPE file '{file.source}' to '{file.filename}'...");
                        File.Copy(file.source, file.filename, true);
                    }

                    _logger.LogInformation("Creating boot scripts...");
                    await File.WriteAllTextAsync(
                        Path.Combine(path.FullName, "autoexec.ipxe"),
                        """
                        #!ipxe
                        dhcp
                        kernel wimboot
                        initrd boot.bat                boot.bat
                        initrd winpeshl.ini            winpeshl.ini
                        initrd uet.exe                 uet.exe
                        initrd BCD                     BCD
                        initrd boot.sdi                boot.sdi
                        initrd boot.wim                boot.wim
                        initrd enroll.ps1              enroll.ps1
                        boot
                        """);
                    await File.WriteAllTextAsync(
                        Path.Combine(path.FullName, "winpeshl.ini"),
                        """
                        [LaunchApps]
                        "boot.bat"
                        """);
                    if (!noAutoUpgrade)
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(path.FullName, "boot.bat"),
                            $"""
                            @echo off
                            echo Starting WinPE for UET agent bootstrap...
                            wpeinit
                            set UET_RUNNING_UNDER_WINPE=1
                            echo Upgrading from version:
                            uet --version
                            uet upgrade
                            move X:\ProgramData\UET\WinPE\uet.exe X:\Windows\System32\uet.exe
                            powershell.exe -ExecutionPolicy Bypass enroll.ps1 -ImagePath "{Path.Combine(path.FullName, "install.wim")}" -ImageIndex "{installWimIndex}" -ProvisioningPackagePath "{Path.Combine(path.FullName, "uet.ppkg")}"
                            """);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(path.FullName, "boot.bat"),
                            $"""
                            @echo off
                            echo Starting WinPE for UET agent bootstrap...
                            wpeinit
                            powershell.exe -ExecutionPolicy Bypass enroll.ps1 -ImagePath "{Path.Combine(path.FullName, "install.wim")}" -ImageIndex "{installWimIndex}" -ProvisioningPackagePath "{Path.Combine(path.FullName, "uet.ppkg")}"
                            """);
                    }
                }

                return 0;
            }
        }
    }
}
