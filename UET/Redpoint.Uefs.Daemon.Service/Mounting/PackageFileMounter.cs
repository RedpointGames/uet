namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Database;
    using Redpoint.Uefs.Daemon.State;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Executors;
    using Redpoint.Uefs.Package;
    using Redpoint.Uefs.Protocol;
    using System.Threading;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal class PackageFileMounter : IMounter<MountPackageFileRequest>
    {
        private readonly ILogger<PackageFileMounter> _logger;
        private readonly IPackageMounterDetector _packageMounterDetector;
        private readonly IWriteScratchPath _writeScratchPath;

        public PackageFileMounter(
            ILogger<PackageFileMounter> logger,
            IPackageMounterDetector packageMounterDetector,
            IWriteScratchPath writeScratchPath)
        {
            _logger = logger;
            _packageMounterDetector = packageMounterDetector;
            _writeScratchPath = writeScratchPath;
        }

        public async Task MountAsync(
            IUefsDaemon daemon,
            MountContext context,
            MountPackageFileRequest request,
            TransactionListenerDelegate onPollingResponse,
            CancellationToken cancellationToken)
        {
            if (daemon.IsPathMountPath(request.MountRequest.MountPath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "There is already another mount at this path."));
            }

            // Run the mount transaction.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new AddMountTransactionRequest
                {
                    MountId = context.MountId,
                    MountRequest = request.MountRequest,
                    MountTypeDebugValue = $"packagefile={request.Path}",
                    IsBeingMountedOnStartup = context.IsBeingMountedOnStartup,
                    MountAsync = async (cancellationToken) =>
                    {
                        // Figure out the mounter to use.
                        IPackageMounter? selectedMounter = _packageMounterDetector.CreateMounterForPackage(request.Path);
                        if (selectedMounter == null)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unable to detect the type of package located at '{request.Path}'. Make sure the file is a valid package."));
                        }

                        Directory.CreateDirectory(request.MountRequest.MountPath);

                        var writeStoragePath = _writeScratchPath.ComputeWriteScratchPath(
                            request.MountRequest,
                            request.Path);
                        _logger.LogInformation($"Mounting {request.Path} at path on host: {request.MountRequest.MountPath}");
                        _logger.LogInformation($"Using write storage path at: {writeStoragePath}");

                        try
                        {
                            await selectedMounter.MountAsync(
                                request.Path,
                                request.MountRequest.MountPath,
                                writeStoragePath,
                                request.MountRequest.WriteScratchPersistence).ConfigureAwait(false);
                        }
                        catch (PackageMounterException ex)
                        {
                            throw new RpcException(new Status(StatusCode.Internal, ex.Message));
                        }
                        catch (Exception ex)
                        {
                            throw new RpcException(new Status(StatusCode.Internal, $"Failed to mount the package: {ex}"));
                        }

                        return (
                            mount: new CurrentPackageUefsMount(
                                request.Path,
                                request.MountRequest.MountPath,
                                null,
                                selectedMounter),
                            persistentMount: new DaemonDatabasePersistentMount
                            {
                                TagHint = null,
                                PackagePath = request.Path,
                                WriteStoragePath = writeStoragePath,
                                PersistenceMode = request.MountRequest.WriteScratchPersistence,
                            }
                        );
                    }
                },
                onPollingResponse,
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var transaction).ConfigureAwait(false))
            {
                await transaction.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
