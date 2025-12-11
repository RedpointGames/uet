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

            public Options()
            {
                Path = new Option<string>("--path") { IsRequired = false };
                Path.AddAlias("-p");
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
                await _packageManager.InstallOrUpgradePackageToLatestAsync("Microsoft.WindowsADK", context.GetCancellationToken());

                // @note: When accepted to WinGet repository, uncomment this.
                // await _packageManager.InstallOrUpgradePackageToLatestAsync("Microsoft.WindowsADK.WinPEAddon", context.GetCancellationToken());

                await using ((await _reservationManagerForUet.ReserveAsync("WinPEPreparation").ConfigureAwait(false))
                    .AsAsyncDisposable(out var temporary)
                    .ConfigureAwait(false))
                {
                    _logger.LogInformation("Cleaning up existing files...");
                    var targetPath = Path.Combine(temporary.ReservedPath, "amd64");
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
                            using (var stream = new FileStream(Path.Combine(temporary.ReservedPath, file.filename), FileMode.Create))
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

                    _logger.LogInformation("Creating boot scripts...");
                    await File.WriteAllTextAsync(
                        Path.Combine(temporary.ReservedPath, "autoexec.ipxe"),
                        """
                        #!ipxe
                        dhcp
                        cpuid --ext 29 && set arch amd64 || set arch x86
                        kernel wimboot
                        initrd boot.bat                                   boot.bat
                        initrd winpeshl.ini                               winpeshl.ini
                        initrd uet.exe                                    uet.exe
                        initrd ${arch}/media/Boot/BCD                     BCD
                        initrd ${arch}/media/Boot/boot.sdi                boot.sdi
                        initrd ${arch}/media/sources/boot.wim             boot.wim
                        boot
                        """);
                    await File.WriteAllTextAsync(
                        Path.Combine(temporary.ReservedPath, "winpeshl.ini"),
                        """
                        [LaunchApps]
                        "boot.bat"
                        """);
                    await File.WriteAllTextAsync(
                        Path.Combine(temporary.ReservedPath, "boot.bat"),
                        """
                        @echo off
                        echo "Starting WinPE for UET agent bootstrap..."
                        wpeinit
                        set UET_RUNNING_UNDER_WINPE=1
                        echo "Upgrading from version: "
                        uet --version
                        uet upgrade
                        X:\ProgramData\UET\UpdatePathForWinPE.bat
                        echo "Now executing version: "
                        uet --version

                        echo "Command prompt is now starting."
                        @echo on
                        cmd
                        """);
                }

                return 0;
            }
        }
    }
}
