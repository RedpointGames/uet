namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading;
    using System.Threading.Tasks;
    using Redpoint.Uefs.Protocol;

    internal sealed class PullGitCommitTransactionExecutor : ITransactionExecutor<PullGitCommitTransactionRequest>
    {
        private readonly ILogger<PullGitCommitTransactionExecutor> _logger;

        public PullGitCommitTransactionExecutor(
            ILogger<PullGitCommitTransactionExecutor> logger)
        {
            _logger = logger;
        }

        public async Task ExecuteTransactionAsync(
            ITransactionContext context,
            PullGitCommitTransactionRequest request,
            CancellationToken cancellationToken)
        {
            // @todo: This should use the path of the Git repo as a uniqueness element
            // of the lock, but we never have multiple Git repos in UEFS at the moment
            // so it's not worth changing the API of IGitRepoManager for.
            using (await context.ObtainLockAsync($"GitPull", cancellationToken).ConfigureAwait(false))
            {
                context.UpdatePollingResponse(x =>
                {
                    x.Init(Protocol.PollingResponseType.Git);
                    x.Starting();
                });

                context.UpdatePollingResponse(x =>
                {
                    x.Error("Git support has been removed from UEFS temporarily while we isolate the cause of AccessViolationExceptions on macOS. If you're actively using Git support in UEFS, let us know at sales@redpoint.games.");
                });
            }
        }
    }
}
