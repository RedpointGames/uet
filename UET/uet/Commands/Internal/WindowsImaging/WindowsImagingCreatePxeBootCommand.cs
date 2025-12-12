namespace UET.Commands.Internal.WindowsImaging
{
    using Fsp;
    using Grpc.Core;
    using k8s.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.AutoDiscovery;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.IO;
    using Redpoint.PackageManagement;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Uet.Workspace.Reservation;
    using RemoteHostApi;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using static RemoteHostApi.RemoteHostService;

    internal class WindowsImagingCreatePxeBootCommand
    {
        public sealed class Options
        {
            public Option<string> Path;
            public Option<bool> NoAutoUpgrade;

            public Options()
            {
                Path = new Option<string>("--path") { IsRequired = true };
                Path.AddAlias("-p");
                NoAutoUpgrade = new Option<bool>("--no-auto-upgrade");
                NoAutoUpgrade.AddAlias("-n");
            }
        }

        public static Command CreateWindowsImagingCreatePxeBootCommand()
        {
            var options = new Options();
            var command = new Command("create-pxe-boot");
            command.AddAllOptions(options);
            command.AddCommonHandler<WindowsImagingCreatePxeBootCommandInstance>(options);
            return command;
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

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var path = context.ParseResult.GetValueForOption(_options.Path)!;
                var noAutoUpgrade = context.ParseResult.GetValueForOption(_options.NoAutoUpgrade)!;

                await _packageManager.InstallOrUpgradePackageToLatestAsync("Microsoft.WindowsADK", context.GetCancellationToken());

                // @note: When accepted to WinGet repository, uncomment this.
                // await _packageManager.InstallOrUpgradePackageToLatestAsync("Microsoft.WindowsADK.WinPEAddon", context.GetCancellationToken());

                await using ((await _reservationManagerForUet.ReserveAsync("WinPEPreparation").ConfigureAwait(false))
                    .AsAsyncDisposable(out var winPeDestination)
                    .ConfigureAwait(false))
                {
                    _logger.LogInformation("Cleaning up existing files...");
                    var targetPath = Path.Combine(winPeDestination.ReservedPath, "amd64");
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

                    Directory.CreateDirectory(path);

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
                            using (var stream = new FileStream(Path.Combine(path, file.filename), FileMode.Create))
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

                    var filesCopy = new (string source, string filename)[]
                    {
                        (Path.Combine(targetPath, "media", "Boot", "BCD"), Path.Combine(path, "BCD")),
                        (Path.Combine(targetPath, "media", "Boot", "boot.sdi"), Path.Combine(path, "boot.sdi")),
                        (Path.Combine(targetPath, "media", "sources", "boot.wim"), Path.Combine(path, "boot.wim")),
                    };
                    foreach (var file in filesCopy)
                    {
                        _logger.LogInformation($"Copying WinPE file '{file.source}' to '{file.filename}'...");
                        File.Copy(file.source, file.filename, true);
                    }

                    _logger.LogInformation("Creating boot scripts...");
                    await File.WriteAllTextAsync(
                        Path.Combine(path, "autoexec.ipxe"),
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
                        boot
                        """);
                    await File.WriteAllTextAsync(
                        Path.Combine(path, "winpeshl.ini"),
                        """
                        [LaunchApps]
                        "boot.bat"
                        """);
                    if (!noAutoUpgrade)
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(path, "boot.bat"),
                            """
                            @echo off
                            echo Starting WinPE for UET agent bootstrap...
                            wpeinit
                            set UET_RUNNING_UNDER_WINPE=1
                            echo Upgrading from version:
                            uet --version
                            uet upgrade
                            move X:\ProgramData\UET\WinPE\uet.exe X:\Windows\System32\uet.exe
                            cmd.exe
                            """);
                    }
                    else
                    {
                        await File.WriteAllTextAsync(
                            Path.Combine(path, "boot.bat"),
                            """
                            @echo off
                            echo Starting WinPE for UET agent bootstrap...
                            wpeinit
                            cmd.exe
                            """);
                    }
                }

                return 0;
            }
        }
    }
}
