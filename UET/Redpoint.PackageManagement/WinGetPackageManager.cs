namespace Redpoint.PackageManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Reservation;
    using System.Runtime.Versioning;
    using System.Text;
    using System.Threading;

    [SupportedOSPlatform("windows")]
    internal class WinGetPackageManager : IPackageManager
    {
        private readonly ILogger<WinGetPackageManager> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;

        public WinGetPackageManager(
            ILogger<WinGetPackageManager> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor,
            ISimpleDownloadProgress simpleDownloadProgress,
            IReservationManagerFactory reservationManagerFactory)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
            _simpleDownloadProgress = simpleDownloadProgress;
            _globalMutexReservationManager = reservationManagerFactory.CreateGlobalMutexReservationManager();
        }

        private async Task<string> FindPwshOrInstallItAsync(CancellationToken cancellationToken)
        {
            // Try to find PowerShell 7 via PATH. The WinGet CLI doesn't work under SYSTEM (even with absolute path) due to MSIX nonsense, but apparently the PowerShell scripts use a COM API that does?
            try
            {
                return await _pathResolver.ResolveBinaryPath("pwsh").ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
            }

            await using (await _globalMutexReservationManager.ReserveExactAsync("PwshInstall", cancellationToken))
            {
                try
                {
                    return await _pathResolver.ResolveBinaryPath("pwsh").ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                }

                _logger.LogInformation($"Downloading PowerShell Core...");
                using var client = new HttpClient();

                var targetPath = Path.Combine(Path.GetTempPath(), "PowerShell-7.4.6-win-x64.msi");
                using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                {
                    await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                        client,
                        new Uri("https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/PowerShell-7.4.6-win-x64.msi"),
                        async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                        cancellationToken).ConfigureAwait(false);
                }

                _logger.LogInformation($"Installing PowerShell Core...");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"C:\WINDOWS\system32\msiexec.exe",
                        Arguments = new LogicalProcessArgument[]
                        {
                            "/a",
                            targetPath,
                            "/quiet",
                            "/qn",
                            "ADD_EXPLORER_CONTEXT_MENU_OPENPOWERSHELL=1",
                            "ADD_FILE_CONTEXT_MENU_RUNPOWERSHELL=1",
                            "ADD_PATH=1",
                            "DISABLE_TELEMETRY=1",
                            "USE_MU=1",
                            "ENABLE_MU=1"
                        },
                        WorkingDirectory = Path.GetTempPath()
                    }, CaptureSpecification.Passthrough, cancellationToken).ConfigureAwait(false);
            }

            return await _pathResolver.ResolveBinaryPath("pwsh").ConfigureAwait(false);
        }

        public async Task InstallOrUpgradePackageToLatestAsync(string packageId, CancellationToken cancellationToken)
        {
            var pwsh = await FindPwshOrInstallItAsync(cancellationToken).ConfigureAwait(false);

            await using (await _globalMutexReservationManager.TryReserveExactAsync($"PackageInstall-{packageId}").ConfigureAwait(false))
            {
                _logger.LogInformation($"Ensuring {packageId} is installed and is up-to-date...");
                var script =
                    $$"""
                    if ($null -eq (Get-InstalledModule -ErrorAction SilentlyContinue -Name Microsoft.WinGet.Client)) {
                        Write-Host "Installing WinGet PowerShell module because it's not currently installed...";
                        Install-Module -Scope CurrentUser -Name Microsoft.WinGet.Client -Force;
                        Import-Module -Name Microsoft.WinGet.Client;
                    }
                    $InstalledPackage = (Get-WinGetPackage -Id {{packageId}} -ErrorAction SilentlyContinue);
                    if ($null -eq $InstalledPackage) {
                        Write-Host "Installing {{packageId}} because it's not currently installed...";
                        Install-WinGetPackage -Id {{packageId}} -Mode Silent;
                        exit 0;
                    } else {
                        $InstalledVersion = $InstalledPackage.InstalledVersion
                        $TargetVersion = (Find-WinGetPackage -Id {{packageId}}).Version
                        if ($InstalledVersion -ne $TargetVersion) {
                            Write-Host "Updating {{packageId}} because the installed version $InstalledVersion is not the target version $TargetVersion...";
                            Update-WinGetPackage -Id {{packageId}} -Mode Silent;
                        }
                        exit 0;
                    }
                    """;
                var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = pwsh,
                        Arguments = [
                            "-NonInteractive",
                            "-OutputFormat",
                            "Text",
                            "-EncodedCommand",
                            encodedScript,
                        ]
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
