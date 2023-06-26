namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading.Tasks;

    internal class RemoveMountTransactionExecutor : ITransactionExecutor<RemoveMountTransactionRequest>
    {
        private readonly IMountTracking _mountTracking;
        private readonly ILogger<RemoveMountTransactionExecutor> _logger;
        private readonly IMountLockObtainer _mountLockObtainer;

        public RemoveMountTransactionExecutor(
            IMountTracking mountTracking,
            ILogger<RemoveMountTransactionExecutor> logger,
            IMountLockObtainer mountLockObtainer)
        {
            _mountTracking = mountTracking;
            _logger = logger;
            _mountLockObtainer = mountLockObtainer;
        }

        public async Task ExecuteTransactionAsync(
            ITransactionContext context,
            RemoveMountTransactionRequest transaction,
            CancellationToken cancellationToken)
        {
            using (await _mountLockObtainer.ObtainLockAsync(
                context,
                "RemoveMountTransactionExecutor",
                cancellationToken))
            {
                if (_mountTracking.CurrentMounts.ContainsKey(transaction.MountId))
                {
                    _logger.LogInformation($"Unmounting {transaction.MountId}...");
                    var mount = _mountTracking.CurrentMounts[transaction.MountId];
                    mount.DisposeUnderlying();
                    if (mount.MountPath != null)
                    {
                        await _mountTracking.RemovePersistentMountAsync(mount.MountPath);
                    }
                    await _mountTracking.RemoveCurrentMountAsync(transaction.MountId);
                }
            }
        }
    }
}
