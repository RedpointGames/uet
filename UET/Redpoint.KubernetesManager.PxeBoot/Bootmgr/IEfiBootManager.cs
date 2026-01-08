namespace Redpoint.KubernetesManager.PxeBoot.Bootmgr
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal interface IEfiBootManager
    {
        Task<EfiBootManagerConfiguration> GetBootManagerConfigurationAsync(
            CancellationToken cancellationToken);

        Task RemoveBootManagerEntryAsync(
            int bootEntry,
            CancellationToken cancellationToken);

        Task AddBootManagerDiskEntryAsync(
            string disk,
            int partition,
            string label,
            string path,
            CancellationToken cancellationToken);

        Task SetBootManagerBootOrderAsync(
            IEnumerable<int> bootOrder,
            CancellationToken cancellationToken);

        Task SetBootManagerEntryActiveAsync(
            int bootEntry,
            bool active,
            CancellationToken cancellationToken);
    }
}
