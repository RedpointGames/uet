namespace Redpoint.PackageManagement
{
    using System.Threading;

    public interface IPackageManager
    {
        /// <summary>
        /// Installs or upgrades the target package to the latest version.
        /// </summary>
        /// <param name="packageId">The package ID (platform dependent).</param>
        /// <param name="locationOverride">If set, overrides the location that the package is installed, if supported.</param>
        /// <param name="cancellationToken">Cancel the installation or upgrade if possible.</param>
        /// <returns>An asynchronous task that can be awaited on.</returns>
        Task InstallOrUpgradePackageToLatestAsync(
            string packageId,
            string? locationOverride = null,
            CancellationToken cancellationToken = default);
    }
}
