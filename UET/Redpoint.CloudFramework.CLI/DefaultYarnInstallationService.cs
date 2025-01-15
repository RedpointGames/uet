namespace Redpoint.CloudFramework.CLI
{
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using System;
    using System.Diagnostics;
    using System.Threading.Tasks;

    internal class DefaultYarnInstallationService : IYarnInstallationService
    {
        private readonly ILogger<DefaultYarnInstallationService> _logger;
        private readonly IPathResolver _pathResolver;
        private readonly IProcessExecutor _processExecutor;

        private static readonly string[] _winGetInstallNodeJsArgs = new[] { "install", "OpenJS.NodeJS" };

        public DefaultYarnInstallationService(
            ILogger<DefaultYarnInstallationService> logger,
            IPathResolver pathResolver,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _pathResolver = pathResolver;
            _processExecutor = processExecutor;
        }

        public async Task<(int exitCode, string? yarnPath)> InstallYarnIfNeededAsync(CancellationToken cancellationToken)
        {
            int exitCode;

            // Check if we have Node.js installed. If we don't, try to use WinGet to install it.
            var node = await _pathResolver.ResolveBinaryPath("node").ConfigureAwait(true);
            var corepack = await _pathResolver.ResolveBinaryPath("corepack").ConfigureAwait(true);
            if (node == null || corepack == null)
            {
                if (OperatingSystem.IsWindows())
                {
                    var winget = await _pathResolver.ResolveBinaryPath("winget").ConfigureAwait(true);
                    if (winget == null)
                    {
                        winget = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe");
                        if (!File.Exists(winget))
                        {
                            winget = null;
                        }
                    }
                    if (winget == null)
                    {
                        if (node != null)
                        {
                            _logger.LogError("WinGet is not installed, so Node.js can't be upgraded automatically. Please install WinGet by installing App Installer from the Microsoft Store first.");
                        }
                        else
                        {
                            _logger.LogError("WinGet is not installed, so Node.js can't be installed automatically. Please install WinGet by installing App Installer from the Microsoft Store first.");
                        }
                        Process.Start("https://www.microsoft.com/p/app-installer/9nblggh4nns1#activetab=pivot:overviewtab");
                        return (1, null);
                    }

                    _logger.LogInformation("Installing Node.js via WinGet...");
                    exitCode = await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = winget,
                            Arguments = _winGetInstallNodeJsArgs.Select(x => new LogicalProcessArgument(x)),
                        },
                        CaptureSpecification.Passthrough,
                        cancellationToken).ConfigureAwait(true);
                    if (exitCode != 0)
                    {
                        _logger.LogError("'winget' command failed; see above for output.");
                        return (exitCode, null);
                    }

                    node = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
                    corepack = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "corepack.cmd");
                    if (!File.Exists(node))
                    {
                        _logger.LogError("Node.js did not install correctly from WinGet, or did not install into the usual place. If Node.js did install correctly, try logging out and logging back in to refresh your environment variables.");
                        return (1, null);
                    }
                    if (!File.Exists(corepack))
                    {
                        _logger.LogError("The version of Node.js that is installed on this machine is not new enough to have 'corepack'. Upgrade Node.js manually and then try again. If Node.js did upgrade correctly, try logging out and logging back in to refresh your environment variables.");
                        return (1, null);
                    }
                }
                else
                {
                    _logger.LogError("Node.js is not installed on this machine, or is not new enough to have the 'corepack' command. Upgrade Node.js to at least v16.9.0 and try again.");
                    return (1, null);
                }
            }

            // Create a temporary location to install the Corepack shims. We'll use this writable location instead of
            // the default since on Windows you can't do 'corepack enable' without elevating to Administrator.
            var corepackShimPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "rcf-corepack");
            var yarnCorepackShimPath = Path.Combine(corepackShimPath, OperatingSystem.IsWindows() ? "yarn.cmd" : "yarn");
            if (!File.Exists(yarnCorepackShimPath))
            {
                _logger.LogInformation("Setting up Node.js corepack shims...");
                Directory.CreateDirectory(corepackShimPath);
                exitCode = await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = corepack,
                        Arguments = new[] { "enable", "--install-directory", corepackShimPath }.Select(x => new LogicalProcessArgument(x)),
                    },
                    CaptureSpecification.Passthrough,
                    cancellationToken).ConfigureAwait(true);
                if (exitCode != 0)
                {
                    _logger.LogError("'corepack enable' command failed; see above for output.");
                    return (exitCode, null);
                }
            }

            return (0, yarnCorepackShimPath);
        }
    }
}
