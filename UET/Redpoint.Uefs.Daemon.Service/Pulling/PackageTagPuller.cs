namespace Redpoint.Uefs.Daemon.Service.Pulling
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Uefs.Protocol;
    using System.Threading;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal sealed class PackageTagPuller : IPuller<PullPackageTagRequest>
    {
        public async Task<PullResult> PullAsync(
            IUefsDaemon daemon,
            PullPackageTagRequest request,
            TransactionListenerDelegate<FileInfo?> onPollingResponse,
            CancellationToken cancellationToken)
        {
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync<PullPackageTagTransactionRequest, PullPackageTagTransactionResult>(
                new PullPackageTagTransactionRequest
                {
                    PackageFs = daemon.PackageStorage.PackageFs,
                    Tag = request.Tag,
                    Credential = request.Credential,
                    NoWait = request.PullRequest.NoWait,
                },
                (response, result) => onPollingResponse(response, result?.PackagePath),
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var transaction).ConfigureAwait(false))
            {
                // If we're not waiting, go as soon as the operation ID is available.
                if (request.PullRequest.NoWait)
                {
                    return new PullResult(transaction.TransactionId, false);
                }

                await transaction.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
                return new PullResult(transaction.TransactionId, true);
            }
        }
    }
}
