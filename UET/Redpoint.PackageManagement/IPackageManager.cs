namespace Redpoint.PackageManagement
{
    using System.Threading;

    public interface IPackageManager
    {
        /// <summary>
        /// Installs or upgrades the target package to the latest version.
        /// </summary>
        /// <param name="packageId">The package ID (platform dependent).</param>
        /// <returns>An asynchronous task that can be awaited on.</returns>
        Task InstallOrUpgradePackageToLatestAsync(
            string packageId,
            CancellationToken cancellationToken);
    }
}
