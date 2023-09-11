namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading;
    using System.Threading.Tasks;
    using Redpoint.Uefs.Protocol;

    internal sealed class VerifyPackagesTransactionExecutor : ITransactionExecutor<VerifyPackagesTransactionRequest>
    {
        public async Task ExecuteTransactionAsync(
            ITransactionContext context,
            VerifyPackagesTransactionRequest transaction,
            CancellationToken cancellationToken)
        {
            context.UpdatePollingResponse(x =>
            {
                x.Init(Protocol.PollingResponseType.Verify);
            });

            var @lock = await context.ObtainLockAsync("PackageStorage", cancellationToken);
            var didReleaseSemaphore = false;
            try
            {
                context.UpdatePollingResponse(x =>
                {
                    x.Starting();
                });

                await transaction.PackageFs.VerifyAsync(
                    transaction.Fix,
                    () =>
                    {
                        @lock.Dispose();
                        didReleaseSemaphore = true;
                    },
                    context.UpdatePollingResponse);

                context.UpdatePollingResponse(x =>
                {
                    x.CompleteForVerifying();
                });
            }
            finally
            {
                if (!didReleaseSemaphore)
                {
                    @lock.Dispose();
                }
            }
        }
    }
}
