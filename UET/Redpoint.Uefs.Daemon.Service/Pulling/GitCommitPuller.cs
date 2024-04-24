namespace Redpoint.Uefs.Daemon.Service.Pulling
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
#if GIT_NATIVE_CODE_ENABLED
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Concurrency;
#endif

    internal sealed class GitCommitPuller : IPuller<PullGitCommitRequest>
    {
#if GIT_NATIVE_CODE_ENABLED
        public async Task<PullResult> PullAsync(
#else
        public Task<PullResult> PullAsync(
#endif
            IUefsDaemon daemon,
            PullGitCommitRequest request,
            TransactionListener<FileInfo?> onPollingResponse,
            CancellationToken cancellationToken)
        {
#if GIT_NATIVE_CODE_ENABLED

            if (daemon.PackageStorage.GitRepoManager == null)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Git commits can not be pulled on this system."));
            }

            // Run pull transaction in case we need to pull this Git commit.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new PullGitCommitTransactionRequest
                {
                    GitRepoManager = daemon.PackageStorage.GitRepoManager,
                    GitUrl = request.Url,
                    GitCommit = request.Commit,
                    Credential = request.Credential,
                    NoWait = request.PullRequest.NoWait,
                },
                response => onPollingResponse(response, null),
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
#else
            throw new RpcException(new Status(StatusCode.Unavailable, "Git commits can not be pulled on this system."));
#endif
        }
    }
}