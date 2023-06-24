namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PullGitCommitTransactionExecutor : ITransactionExecutor<PullGitCommitTransactionRequest>
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
            using (await context.ObtainLockAsync($"GitPull", cancellationToken))
            {
                context.UpdatePollingResponse(x =>
                {
                    x.Init(Protocol.PollingResponseType.Git);
                    x.Starting();
                });

                if (request.GitRepoManager.HasCommit(request.GitCommit))
                {
                    // This commit is already present, we're good to go.
                    context.UpdatePollingResponse(x =>
                    {
                        x.CompleteForGit();
                    });
                    return;
                }

                context.UpdatePollingResponse(x =>
                {
                    x.PullingGit();
                });

                // Otherwise, try to fetch it.
                var publicKey = Path.GetTempFileName();
                var privateKey = Path.GetTempFileName();
                try
                {
                    File.WriteAllText(publicKey, request.Credential.SshPublicKeyAsPem);
                    File.WriteAllText(privateKey, request.Credential.SshPrivateKeyAsPem);
                    await request.GitRepoManager.Fetch(
                        request.GitUrl,
                        request.GitCommit,
                        publicKey,
                        privateKey,
                        progress =>
                        {
                            context.UpdatePollingResponse(op =>
                            {
                                op.ReceiveGitUpdate(progress);
                            });
                        });
                }
                finally
                {
                    try
                    {
                        File.Delete(publicKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to delete public key from disk: {ex.Message}");
                    }
                    try
                    {
                        File.Delete(privateKey);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to delete private key from disk: {ex.Message}");
                    }
                }

                context.UpdatePollingResponse(op =>
                {
                    op.CompleteForGit();
                });
            }
        }
    }
}
