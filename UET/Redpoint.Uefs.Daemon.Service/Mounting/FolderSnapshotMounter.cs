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
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Driver;
    using Redpoint.Vfs.Layer.Folder;
    using Redpoint.Vfs.Layer.Scratch;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal sealed class FolderSnapshotMounter : IMounter<MountFolderSnapshotRequest>
    {
        private readonly ILogger<FolderSnapshotMounter> _logger;
        private readonly IFolderVfsLayerFactory _folderVfsLayerFactory;
        private readonly IScratchVfsLayerFactory _scratchVfsLayerFactory;
        private readonly IWriteScratchPath _writeScratchPath;
        private readonly IVfsDriverFactory? _vfsDriverFactory;

        public FolderSnapshotMounter(
            ILogger<FolderSnapshotMounter> logger,
            IFolderVfsLayerFactory folderVfsLayerFactory,
            IScratchVfsLayerFactory scratchVfsLayerFactory,
            IWriteScratchPath writeScratchPath,
            IVfsDriverFactory? vfsDriverFactory = null)
        {
            _logger = logger;
            _folderVfsLayerFactory = folderVfsLayerFactory;
            _scratchVfsLayerFactory = scratchVfsLayerFactory;
            _writeScratchPath = writeScratchPath;
            _vfsDriverFactory = vfsDriverFactory;
        }

        public async Task MountAsync(
            IUefsDaemon daemon,
            MountContext context,
            MountFolderSnapshotRequest request,
            TransactionListener onPollingResponse,
            CancellationToken cancellationToken)
        {
            if (_vfsDriverFactory == null)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Folder snapshots can not be mounted on this system."));
            }

            if (string.IsNullOrWhiteSpace(request.SourcePath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "The SourcePath must not be an empty string."));
            }

            if (!Directory.Exists(request.SourcePath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "The SourcePath must be an existing directory."));
            }

            // Run the mount transaction.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new AddMountTransactionRequest
                {
                    MountId = context.MountId,
                    MountRequest = request.MountRequest,
                    MountTypeDebugValue = $"folder={request.SourcePath}",
                    IsBeingMountedOnStartup = context.IsBeingMountedOnStartup,
                    MountAsync = (cancellationToken) =>
                    {
                        _logger.LogInformation($"Mounting '{request.SourcePath}' as folder snapshot at path on host: {request.MountRequest.MountPath}");

                        // Set up the source layer.
                        IVfsLayer finalLayer;
                        finalLayer = _folderVfsLayerFactory.CreateLayer(
                            request.SourcePath,
                            null);

                        // Set up the scratch layer and VFS.
                        var writeScratchPath = _writeScratchPath.ComputeWriteScratchPath(
                            request.MountRequest,
                            request.MountRequest.WriteScratchPath);
                        _logger.LogInformation($"Mounting folder snapshot using WinFSP VFS, with write storage path at: {writeScratchPath}");
                        finalLayer = _scratchVfsLayerFactory.CreateLayer(
                            writeScratchPath,
                            finalLayer);

                        // Mount the VFS.
                        var vfs = _vfsDriverFactory.InitializeAndMount(
                            finalLayer,
                            request.MountRequest.MountPath,
                            null)!;
                        if (vfs == null)
                        {
                            throw new RpcException(new Status(StatusCode.Internal, $"Unable to mount folder snapshot. Ensure WinFsp is installed on this system."));
                        }

                        return Task.FromResult<(CurrentUefsMount mount, DaemonDatabasePersistentMount persistentMount)>((
                            mount: new CurrentFolderSnapshotUefsMount(
                                request.SourcePath,
                                request.MountRequest.MountPath,
                                vfs)
                            {
                                WriteScratchPersistence = request.MountRequest.WriteScratchPersistence,
                                StartupBehaviour = request.MountRequest.StartupBehaviour,
                                TrackPid = context.TrackedPid,
                            },
                            persistentMount: new DaemonDatabasePersistentMount
                            {
                                FolderSnapshotSourcePath = request.SourcePath,
                                WriteStoragePath = writeScratchPath,
                                PersistenceMode = request.MountRequest.WriteScratchPersistence,
                            }));
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
