namespace UET.Commands.Cluster
{
    using Microsoft.Extensions.Logging;
    using Redpoint.KubernetesManager.Abstractions;
    using Redpoint.ProgressMonitor;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using UET.Commands.Upgrade;

    internal class UetRkmSelfUpgradeService : IRkmSelfUpgradeService
    {
        private readonly IProgressFactory _progressFactory;
        private readonly IMonitorFactory _monitorFactory;
        private readonly ILogger<UetRkmSelfUpgradeService> _logger;

        public UetRkmSelfUpgradeService(
            IProgressFactory progressFactory,
            IMonitorFactory monitorFactory,
            ILogger<UetRkmSelfUpgradeService> logger)
        {
            _progressFactory = progressFactory;
            _monitorFactory = monitorFactory;
            _logger = logger;
        }

        public async Task<bool> UpgradeIfNeededAsync(CancellationToken cancellationToken)
        {
            var upgradeResult = await UpgradeCommandImplementation.PerformUpgradeAsync(
                _progressFactory,
                _monitorFactory,
                _logger,
                string.Empty,
                false,
                false,
                cancellationToken).ConfigureAwait(false);
            return upgradeResult.CurrentVersionWasChanged;
        }
    }
}
