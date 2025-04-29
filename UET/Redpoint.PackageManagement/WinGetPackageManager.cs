namespace Redpoint.PackageManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Reservation;
    using System.Diagnostics;
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
                var stopwatch = Stopwatch.StartNew();
                _logger.LogInformation($"Ensuring {packageId} is installed and up-to-date...");
                try
                {
                    var script =
                        $$"""
                        $ErrorActionPreference = "Stop";

                        $PackageId = "{{packageId}}";

                        if ($null -eq (Get-Module -ListAvailable -Name Microsoft.WinGet.Client -ErrorAction SilentlyContinue)) {
                            Write-Host "Installing WinGet PowerShell module because it's not currently installed...";
                            Install-Module -Scope CurrentUser -Name Microsoft.WinGet.Client -Force;
                            Import-Module -Name Microsoft.WinGet.Client;
                        }

                        $InstalledVersionPath = "$env:USERPROFILE\$PackageId.pkgiv";
                        $InstalledVersionFile = Get-Item -Path $InstalledVersionPath -ErrorAction SilentlyContinue;
                        $InstalledVersionStopwatch = [System.Diagnostics.Stopwatch]::StartNew();
                        $InstalledVersion = $null;
                        if ($null -eq $InstalledVersionFile -or
                            ((Get-Date) - $InstalledVersionFile.LastWriteTime).TotalMinutes -gt 10) {
                            $InstalledPackage = (Get-WinGetPackage -Id $PackageId -ErrorAction SilentlyContinue);
                            if ($null -eq $InstalledPackage) {
                                $InstalledVersion = $null;
                            } else {
                                $InstalledVersion = $InstalledPackage.InstalledVersion;
                            }
                            try {
                                Set-Content -Path $InstalledVersionPath -Value "$InstalledVersion";
                            } catch {
                            }
                            Write-Host "Detected installed version of $PackageId as '$InstalledVersion' in $($InstalledVersionStopwatch.Elapsed.TotalSeconds.ToString("F2")) seconds."
                        } else {
                            $InstalledVersion = (Get-Content -Path $InstalledVersionFile.FullName -Raw).Trim();
                            Write-Host "Detected installed version of $PackageId as '$InstalledVersion' in $($InstalledVersionStopwatch.Elapsed.TotalSeconds.ToString("F2")) seconds (from cache)."
                        }
                        
                        $TargetVersionPath = "$env:USERPROFILE\$PackageId.pkgtv";
                        $TargetVersionFile = Get-Item -Path $TargetVersionPath -ErrorAction SilentlyContinue;
                        $TargetVersionStopwatch = [System.Diagnostics.Stopwatch]::StartNew();
                        $TargetVersion = $null;
                        if ($null -eq $TargetVersionFile -or
                            ((Get-Date) - $TargetVersionFile.LastWriteTime).TotalMinutes -gt 60) {
                            $TargetVersion = ((Find-WinGetPackage -Id {{packageId}}).Version | Select-Object -First 1);
                            try {
                                Set-Content -Path $TargetVersionPath -Value "$TargetVersion";
                            } catch {
                            }
                            Write-Host "Detected target version of $PackageId as '$TargetVersion' in $($TargetVersionStopwatch.Elapsed.TotalSeconds.ToString("F2")) seconds."
                        } else {
                            $TargetVersion = (Get-Content -Path $TargetVersionFile.FullName -Raw).Trim();
                            Write-Host "Detected target version of $PackageId as '$TargetVersion' in $($TargetVersionStopwatch.Elapsed.TotalSeconds.ToString("F2")) seconds (from cache)."
                        }

                        if ($null -eq $InstalledVersion -or $InstalledVersion -eq "") {
                            Write-Host "Installing $PackageId because it's not currently installed...";
                            Install-WinGetPackage -Id $PackageId -Mode Silent;
                        }
                        elseif ($InstalledVersion -ne $TargetVersion) {
                            Write-Host "Updating $PackageId because the installed version $InstalledVersion is not the target version $TargetVersion...";
                            Update-WinGetPackage -Id $PackageId -Mode Silent;
                        }
                        exit 0;
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
                finally
                {
                    _logger.LogInformation($"Took {stopwatch.Elapsed.TotalSeconds:F2} seconds to ensure {packageId} is installed and up-to-date.");
                }
            }
        }
    }
}
