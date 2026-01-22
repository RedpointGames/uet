namespace Redpoint.PackageManagement
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Reservation;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    [SupportedOSPlatform("macos")]
    internal class HomebrewPackageManager : IPackageManager
    {
        private readonly ILogger<HomebrewPackageManager> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;

        public HomebrewPackageManager(
            ILogger<HomebrewPackageManager> logger,
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

        private async Task<string> FindHomebrewOrInstallItAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _pathResolver.ResolveBinaryPath("brew").ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
            }

            await using (await _globalMutexReservationManager.ReserveExactAsync("HomebrewInstall", cancellationToken))
            {
                try
                {
                    return await _pathResolver.ResolveBinaryPath("brew").ConfigureAwait(false);
                }
                catch (FileNotFoundException)
                {
                }

                if (File.Exists("/opt/homebrew/bin/brew"))
                {
                    return "/opt/homebrew/bin/brew";
                }

                _logger.LogInformation($"Downloading Homebrew...");
                using var client = new HttpClient();

                var targetPath = Path.Combine(Path.GetTempPath(), "install.sh");
                using (var file = new FileStream(targetPath, FileMode.Create, FileAccess.Write))
                {
                    await _simpleDownloadProgress.DownloadAndCopyToStreamAsync(
                        client,
                        new Uri("https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh"),
                        async stream => await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false),
                        cancellationToken).ConfigureAwait(false);
                }
                File.SetUnixFileMode(targetPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

                _logger.LogInformation($"Installing Homebrew...");
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = @"/bin/bash",
                        Arguments = new LogicalProcessArgument[]
                        {
                            "-c",
                            targetPath,
                        },
                        WorkingDirectory = Path.GetTempPath(),
                        EnvironmentVariables = new Dictionary<string, string>
                        {
                            { "NONINTERACTIVE", "1" }
                        },
                    }, CaptureSpecification.Passthrough, cancellationToken).ConfigureAwait(false);

                return "/opt/homebrew/bin/brew";
            }
        }

        public async Task InstallOrUpgradePackageToLatestAsync(
            string packageId,
            string? locationOverride,
            CancellationToken cancellationToken)
        {
            var homebrew = await FindHomebrewOrInstallItAsync(cancellationToken).ConfigureAwait(false);

            await using (await _globalMutexReservationManager.TryReserveExactAsync($"PackageInstall-{packageId}").ConfigureAwait(false))
            {
                _logger.LogInformation($"Checking if {packageId} is installed...");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = homebrew,
                        Arguments = new LogicalProcessArgument[]
                        {
                            "list",
                            packageId,
                        },
                        WorkingDirectory = Path.GetTempPath(),
                        EnvironmentVariables = new Dictionary<string, string>
                        {
                            { "NONINTERACTIVE", "1" }
                        },
                    }, CaptureSpecification.Silence, cancellationToken).ConfigureAwait(false);

                if (exitCode == 0)
                {
                    _logger.LogInformation($"Ensuring {packageId} is up-to-date...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = homebrew,
                            Arguments = [
                                "upgrade",
                                packageId,
                            ],
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "NONINTERACTIVE", "1" }
                            },
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    _logger.LogInformation($"Installing {packageId}...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = homebrew,
                            Arguments = [
                                "install",
                                packageId,
                            ],
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "NONINTERACTIVE", "1" }
                            },
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}
