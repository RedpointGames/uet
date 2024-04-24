namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Driver;
    using System.Threading;
    using System.Threading.Tasks;
#if GIT_NATIVE_CODE_ENABLED
    using Redpoint.Uefs.Daemon.Database;
    using Redpoint.Uefs.Daemon.State;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Concurrency;
#endif

    internal sealed class GitHubCommitMounter : IMounter<MountGitHubCommitRequest>
    {
        private readonly ILogger<GitHubCommitMounter> _logger;
#if GIT_NATIVE_CODE_ENABLED
        private readonly IGitVfsLayerFactory _gitVfsLayerFactory;
        private readonly IGitVfsSetup _gitVfsSetup;
#endif
        private readonly IVfsDriverFactory? _vfsDriverFactory;

        public GitHubCommitMounter(
            ILogger<GitHubCommitMounter> logger,
#if GIT_NATIVE_CODE_ENABLED
            IGitVfsLayerFactory gitVfsLayerFactory,
            IGitVfsSetup gitVfsSetup,
#endif
            IVfsDriverFactory? vfsDriverFactory = null)
        {
            _logger = logger;
#if GIT_NATIVE_CODE_ENABLED
            _gitVfsLayerFactory = gitVfsLayerFactory;
            _gitVfsSetup = gitVfsSetup;
#endif
            _vfsDriverFactory = vfsDriverFactory;
        }

#if GIT_NATIVE_CODE_ENABLED
        public async Task MountAsync(
#else
        public Task MountAsync(
#endif
            IUefsDaemon daemon,
            MountContext context,
            MountGitHubCommitRequest request,
            TransactionListener onPollingResponse,
            CancellationToken cancellationToken)
        {
#if GIT_NATIVE_CODE_ENABLED
            if (daemon.IsPathMountPath(request.MountRequest.MountPath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "There is already another mount at this path."));
            }

            if (_vfsDriverFactory == null)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Git commits can not be mounted on this system."));
            }

            // Run the mount transaction.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new AddMountTransactionRequest
                {
                    MountId = context.MountId,
                    MountRequest = request.MountRequest,
                    MountTypeDebugValue = $"github={request.Owner}/{request.Repo}?{request.Commit}",
                    IsBeingMountedOnStartup = context.IsBeingMountedOnStartup,
                    MountAsync = async (cancellationToken) =>
                    {
                        _logger.LogInformation($"Mounting '{request.Commit}' from GitHub repo '{request.Owner}/{request.Repo}' at path on host: {request.MountRequest.MountPath}");

                        // Git layer with source code.
                        var gitLayer = _gitVfsLayerFactory.CreateGitHubLayer(
                            request.Credential.Token,
                            request.Owner,
                            request.Repo,
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
                            "GitHub mount").ConfigureAwait(false);

                        return (
                            mount: new CurrentGitHubUefsMount(
                                request.Owner,
                                request.Repo,
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
                                GitHubOwner = request.Owner,
                                GitHubRepo = request.Repo,
                                GitHubToken = request.Credential.Token,
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
#else
            throw new RpcException(new Status(StatusCode.Unavailable, "Support for mounting Git commits has been temporarily removed from UEFS."));
#endif
        }
    }
}