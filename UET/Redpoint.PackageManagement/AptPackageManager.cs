using Microsoft.Extensions.Logging;
using Redpoint.PathResolution;
using Redpoint.ProcessExecution;
using Redpoint.Reservation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading.Tasks;

namespace Redpoint.PackageManagement
{
    [SupportedOSPlatform("linux")]
    internal class AptPackageManager : IPackageManager
    {
        private readonly ILogger<AptPackageManager> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IPathResolver _pathResolver;
        private readonly IGlobalMutexReservationManager _globalMutexReservationManager;

        public AptPackageManager(
            ILogger<AptPackageManager> logger,
            IProcessExecutor processExecutor,
            IPathResolver pathResolver,
            IReservationManagerFactory reservationManagerFactory)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _pathResolver = pathResolver;
            _globalMutexReservationManager = reservationManagerFactory.CreateGlobalMutexReservationManager();
        }

        [DllImport("libc")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        public static extern uint getuid();

        public async Task InstallOrUpgradePackageToLatestAsync(string packageId, CancellationToken cancellationToken)
        {
            if (getuid() == 0)
            {
                _logger.LogWarning($"Unable to ensure {packageId} is installed or up-to-date as you are not running as root.");
                return;
            }

            var aptGet = await _pathResolver.ResolveBinaryPath("apt-get");
            var dpkgQuery = await _pathResolver.ResolveBinaryPath("dpkg-query");

            await using (await _globalMutexReservationManager.TryReserveExactAsync($"PackageListUpdate").ConfigureAwait(false))
            {
                _logger.LogError($"Refreshing package list via 'apt-get update'...");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = aptGet,
                        Arguments = ["update"],
                        EnvironmentVariables = new Dictionary<string, string>
                        {
                            { "DEBIAN_FRONTEND", "noninteractive" },
                        }
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken);
                if (exitCode != 0)
                {
                    _logger.LogError($"'apt-get update' exited with non-zero exit code: {exitCode}");
                    return;
                }
            }

            await using (await _globalMutexReservationManager.TryReserveExactAsync($"PackageInstall-{packageId}").ConfigureAwait(false))
            {
                _logger.LogInformation($"Checking if {packageId} is installed...");
                var exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = dpkgQuery,
                        Arguments = new LogicalProcessArgument[]
                        {
                            "-W",
                            packageId,
                        },
                        WorkingDirectory = Path.GetTempPath(),
                        EnvironmentVariables = new Dictionary<string, string>
                        {
                            { "DEBIAN_FRONTEND", "noninteractive" },
                        },
                    }, CaptureSpecification.Silence, cancellationToken).ConfigureAwait(false);

                if (exitCode == 0)
                {
                    _logger.LogInformation($"Ensuring {packageId} is up-to-date...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = aptGet,
                            Arguments = [
                                "upgrade",
                                "-y",
                                packageId,
                            ],
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "DEBIAN_FRONTEND", "noninteractive" },
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
                            FilePath = aptGet,
                            Arguments = [
                                "install",
                                "-y",
                                packageId,
                            ],
                            EnvironmentVariables = new Dictionary<string, string>
                            {
                                { "DEBIAN_FRONTEND", "noninteractive" },
                            },
                        },
                        CaptureSpecification.Passthrough,
                        CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
    }
}
