namespace Redpoint.PackageManagement
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class NullPackageManager : IPackageManager
    {
        public Task InstallOrUpgradePackageToLatestAsync(
            string packageId,
            string? locationOverride,
            CancellationToken cancellationToken)
        {
            throw new PlatformNotSupportedException("This platform does not support package management.");
        }
    }
}
