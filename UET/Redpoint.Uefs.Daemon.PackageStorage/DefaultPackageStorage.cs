namespace Redpoint.Uefs.Daemon.PackageStorage
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Git.Native;
    using Redpoint.Uefs.Daemon.PackageFs;
    using System.ServiceProcess;

    internal class DefaultPackageStorage : IPackageStorage
    {
        private readonly ILogger<DefaultPackageStorage> _logger;
        private readonly IGitRepoManager _gitRepoManager;
        private readonly IPackageFs _packageFs;

        public DefaultPackageStorage(
            ILogger<DefaultPackageStorage> logger,
            IGitRepoManagerFactory gitRepoManagerFactory,
            IPackageFsFactory packageFsFactory,
            string storagePath)
        {
            _logger = logger;
            _gitRepoManager = gitRepoManagerFactory.CreateGitRepoManager(
                Path.Combine(storagePath, "git-repo"));

            if (OperatingSystem.IsWindows())
            {
                if (OperatingSystem.IsWindowsVersionAtLeast(6, 2))
                {
#pragma warning disable CA1416 // Validate platform compatibility
                    if (ServiceController.GetServices().Any(x => x.ServiceName == "WinFsp.Launcher"))
#pragma warning restore CA1416 // Validate platform compatibility
                    {
                        // WinFSP is available, use that instead of Dokan.
                        _logger.LogInformation("Selecting WinFSP as the virtual filesystem provider for instant packages.");

                        try
                        {
                            _packageFs = packageFsFactory.CreateVfsBackedPackageFs(storagePath);
                        }
                        catch (FileNotFoundException ex) when (ex.Message.Contains("winfsp-msil"))
                        {
                            logger.LogWarning("WinFSP is not correctly installed on this system, so UEFS will fall back to full downloads on Windows. Please try restarting the machine or re-installing the WinFSP driver.");
                            _packageFs = packageFsFactory.CreateLocallyBackedPackageFs(storagePath);
                        }
                    }
                    else
                    {
                        logger.LogWarning("WinFsp is not installed on this system, so UEFS will fall back to full downloads on Windows. Install WinFsp 2023 RC1 or later from https://github.com/winfsp/winfsp/releases to use on-demand data fetching.");
                        _packageFs = packageFsFactory.CreateLocallyBackedPackageFs(storagePath);
                    }
                }
                else
                {
                    throw new PlatformNotSupportedException();
                }
            }
            else
            {
                _packageFs = packageFsFactory.CreateLocallyBackedPackageFs(storagePath);
            }
        }

        public IPackageFs PackageFs => _packageFs;

        public IGitRepoManager GitRepoManager => _gitRepoManager;

        public void StopProcesses()
        {
            _logger.LogInformation($"Shutting down the virtual file system...");
            _packageFs.Dispose();

            _logger.LogInformation($"Stopping Git processes operating in the Git repository..");
            _gitRepoManager.StopProcesses();
        }
    }
}
