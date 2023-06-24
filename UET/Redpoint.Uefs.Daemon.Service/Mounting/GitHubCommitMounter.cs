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
    using System.Threading;
    using System.Threading.Tasks;

    internal class GitHubCommitMounter : IMounter<MountGitHubCommitRequest>
    {
        private readonly ILogger<GitHubCommitMounter> _logger;
        private readonly IGitVfsLayerFactory _gitVfsLayerFactory;
        private readonly IGitVfsSetup _gitVfsSetup;
        private readonly IVfsDriverFactory? _vfsDriverFactory;

        public GitHubCommitMounter(
            ILogger<GitHubCommitMounter> logger,
            IGitVfsLayerFactory gitVfsLayerFactory,
            IGitVfsSetup gitVfsSetup,
            IVfsDriverFactory? vfsDriverFactory = null)
        {
            _logger = logger;
            _gitVfsLayerFactory = gitVfsLayerFactory;
            _gitVfsSetup = gitVfsSetup;
            _vfsDriverFactory = vfsDriverFactory;
        }

        public async Task MountAsync(
            IUefsDaemon daemon,
            MountContext context,
            MountGitHubCommitRequest request,
            TransactionListenerDelegate onPollingResponse,
            CancellationToken cancellationToken)
        {
            if (daemon.IsPathMountPath(request.MountRequest.MountPath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "There is already another mount at this path."));
            }

            if (_vfsDriverFactory == null)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Git commits can not be mounted on this system."));
            }

            // Run the mount transaction.
            await using (var transaction = await daemon.TransactionalDatabase.BeginTransactionAsync(
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
                        await gitLayer.InitAsync(CancellationToken.None);

                        var (writeScratchPath, vfs) = await _gitVfsSetup.MountAsync(
                            daemon,
                            request.MountRequest,
                            Path.Combine(daemon.StoragePath, "git-repo"),
                            request.FolderLayers.ToArray(),
                            gitLayer,
                            "GitHub mount");

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
                cancellationToken))
            {
                await transaction.WaitForCompletionAsync(cancellationToken);
            }
        }
    }
}
