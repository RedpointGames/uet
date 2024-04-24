#if GIT_NATIVE_CODE_ENABLED

namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Database;
    using Redpoint.Uefs.Daemon.State;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;
    using Redpoint.Vfs.Layer.Git;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal sealed class GitCommitMounter : IMounter<MountGitCommitRequest>
    {
        private readonly ILogger<GitCommitMounter> _logger;
        private readonly IVfsDriverFactory? _vfsDriverFactory;
        private readonly IGitVfsLayerFactory _gitVfsLayerFactory;
        private readonly IGitVfsSetup _gitVfsSetup;

        public GitCommitMounter(
            ILogger<GitCommitMounter> logger,
            IGitVfsLayerFactory gitVfsLayerFactory,
            IGitVfsSetup gitVfsSetup,
            IVfsDriverFactory? vfsDriverFactory = null)
        {
            _logger = logger;
            _vfsDriverFactory = vfsDriverFactory;
            _gitVfsLayerFactory = gitVfsLayerFactory;
            _gitVfsSetup = gitVfsSetup;
        }

        public async Task MountAsync(
            IUefsDaemon daemon,
            MountContext context,
            MountGitCommitRequest request,
            TransactionListener onPollingResponse,
            CancellationToken cancellationToken)
        {
            if (daemon.IsPathMountPath(request.MountRequest.MountPath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "There is already another mount at this path."));
            }

            if (_vfsDriverFactory == null || daemon.PackageStorage.GitRepoManager == null)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Git commits can not be mounted on this system."));
            }

            // Run pull transaction in case we need to pull this Git commit.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new PullGitCommitTransactionRequest
                {
                    GitRepoManager = daemon.PackageStorage.GitRepoManager,
                    GitUrl = request.Url,
                    GitCommit = request.Commit,
                    Credential = request.Credential,
                    NoWait = false,
                },
                async response =>
                {
                    // Don't propagate the completion status, because we still have the mount to do.
                    if (response.Status != PollingResponseStatus.Complete)
                    {
                        await onPollingResponse(response).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var transaction).ConfigureAwait(false))
            {
                await transaction.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
            }

            // Now run the mount transaction.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new AddMountTransactionRequest
                {
                    MountId = context.MountId,
                    MountRequest = request.MountRequest,
                    MountTypeDebugValue = $"git={request.Url}?{request.Commit}",
                    IsBeingMountedOnStartup = context.IsBeingMountedOnStartup,
                    MountAsync = async (cancellationToken) =>
                    {
                        _logger.LogInformation($"Mounting '{request.Commit}' from '{request.Url}' at path on host: {request.MountRequest.MountPath}");

                        // Git layer with source code.
                        var gitLayer = _gitVfsLayerFactory.CreateNativeLayer(
                            Path.Combine(daemon.StoragePath, "git-repo"),
                            Path.Combine(daemon.StoragePath, "git-blob"),
                            Path.Combine(daemon.StoragePath, "git-index-cache"),
                            request.Commit);
                        await gitLayer.InitAsync(CancellationToken.None).ConfigureAwait(false);

                        var (writeScratchPath, vfs) = await _gitVfsSetup.MountAsync(
                            daemon,
                            request.MountRequest,
                            Path.Combine(daemon.StoragePath, "git-repo"),
                            request.FolderLayers.ToArray(),
                            gitLayer,
                            "Git mount").ConfigureAwait(false);

                        return (
                            mount: new CurrentGitUefsMount(
                                request.Url,
                                request.Commit,
                                request.MountRequest.MountPath,
                                vfs)
                            {
                                WriteScratchPersistence = request.MountRequest.WriteScratchPersistence,
                                StartupBehaviour = request.MountRequest.StartupBehaviour,
                                TrackPid = context.TrackedPid,
                            },
                            persistentMount: new DaemonDatabasePersistentMount
                            {
                                GitUrl = request.Url,
                                GitCommit = request.Commit,
                                GitWithLayers = request.FolderLayers.ToArray(),
                                WriteStoragePath = writeScratchPath,
                                PersistenceMode = request.MountRequest.WriteScratchPersistence,
                            });
                    },
                },
                onPollingResponse,
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var transaction).ConfigureAwait(false))
            {
                await transaction.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

#endif