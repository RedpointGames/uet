namespace Redpoint.KubernetesManager.Abstractions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public interface IRkmSelfUpgradeService
    {
        Task<bool> UpgradeIfNeededAsync(CancellationToken cancellationToken);
    }
}
