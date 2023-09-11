namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal sealed class AddMountTransactionExecutor : ITransactionExecutor<AddMountTransactionRequest>
    {
        private readonly IMountTracking _mountTracking;
        private readonly ILogger<AddMountTransactionExecutor> _logger;
        private readonly IMountLockObtainer _mountLockObtainer;

        public AddMountTransactionExecutor(
            IMountTracking mountTracking,
            ILogger<AddMountTransactionExecutor> logger,
            IMountLockObtainer mountLockObtainer)
        {
            _mountTracking = mountTracking;
            _logger = logger;
            _mountLockObtainer = mountLockObtainer;
        }

        public async Task ExecuteTransactionAsync(
            ITransactionContext context,
            AddMountTransactionRequest transaction,
            CancellationToken cancellationToken)
        {
            using (await _mountLockObtainer.ObtainLockAsync(
                context,
                $"AddMountTransactionExecutor:{transaction.MountTypeDebugValue}",
                cancellationToken).ConfigureAwait(false))
            {
                Directory.CreateDirectory(transaction.MountRequest.MountPath);

                Process? trackedPid = null;
                if (transaction.MountRequest.TrackPid != 0)
                {
                    trackedPid = Process.GetProcessById(transaction.MountRequest.TrackPid);
                    if (trackedPid == null)
                    {
                        _logger.LogInformation($"Not mounting {transaction.MountId} because PID {transaction.MountRequest.TrackPid} has already exited or can't be tracked.");
                        throw new RpcException(new Status(StatusCode.Aborted, $"The process tracked (PID {transaction.MountRequest.TrackPid}) for this mount has already exited by the time the mount was being processed."));
                    }
                }

                var (mount, persistentMount) = await transaction.MountAsync(cancellationToken).ConfigureAwait(false);
                mount.WriteScratchPersistence = transaction.MountRequest.WriteScratchPersistence;
                mount.StartupBehaviour = transaction.MountRequest.StartupBehaviour;

                if (trackedPid != null)
                {
                    var didRaceExit = false;
                    mount.TrackPid = trackedPid.Id;
                    trackedPid.Exited += async (sender, args) =>
                    {
                        didRaceExit = true;
                        _logger.LogInformation($"Automatically unmounting {transaction.MountId} because PID {trackedPid.Id} has exited.");
                        await using ((await context.Database.BeginTransactionAsync(
                            new RemoveMountTransactionRequest
                            {
                                MountId = transaction.MountId
                            },
                            _ => Task.CompletedTask,
                            CancellationToken.None).ConfigureAwait(false)).AsAsyncDisposable(out var unmountTransaction).ConfigureAwait(false))
                        {
                            await unmountTransaction.WaitForCompletionAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                    };
                    trackedPid.EnableRaisingEvents = true;
                    if (trackedPid.HasExited && !didRaceExit)
                    {
                        _logger.LogInformation($"Automatically unmounting {transaction.MountId} because PID {trackedPid.Id} has exited.");
                        // It never made it to the list of tracked mounts.
                        mount.DisposeUnderlying();
                        throw new RpcException(new Status(StatusCode.Aborted, $"The process tracked (PID {transaction.MountRequest.TrackPid}) for this mount has exited during the time when the mount was being processed."));
                    }
                }

                if (transaction.MountRequest.StartupBehaviour == StartupBehaviour.MountOnStartup)
                {
                    await _mountTracking.AddPersistentMountAsync(
                        transaction.MountRequest.MountPath,
                        persistentMount).ConfigureAwait(false);
                }

                await _mountTracking.AddCurrentMountAsync(transaction.MountId, mount).ConfigureAwait(false);
            }
        }
    }
}
