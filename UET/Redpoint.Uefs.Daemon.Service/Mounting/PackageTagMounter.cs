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
    using System;
    using System.Threading.Tasks;
    using Redpoint.Concurrency;

    internal sealed class PackageTagMounter : IMounter<MountPackageTagRequest>
    {
        private readonly ILogger<PackageTagMounter> _logger;
        private readonly IPackageMounterDetector _packageMounterDetector;
        private readonly IWriteScratchPath _writeScratchPath;

        public PackageTagMounter(
            ILogger<PackageTagMounter> logger,
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
            MountPackageTagRequest request,
            TransactionListener onPollingResponse,
            CancellationToken cancellationToken)
        {
            if (daemon.IsPathMountPath(request.MountRequest.MountPath))
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, "There is already another mount at this path."));
            }

            // Run pull transaction in case we need to pull this package.
            PullPackageTagTransactionResult result;
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync<PullPackageTagTransactionRequest, PullPackageTagTransactionResult>(
                new PullPackageTagTransactionRequest
                {
                    PackageFs = daemon.PackageStorage.PackageFs,
                    Tag = request.Tag,
                    Credential = request.Credential,
                    NoWait = false,
                },
                async (response, _) =>
                {
                    // Don't propagate the completion status, because we still have the mount to do.
                    if (response.Status != PollingResponseStatus.Complete)
                    {
                        await onPollingResponse(response).ConfigureAwait(false);
                    }
                },
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var transaction).ConfigureAwait(false))
            {
                result = await transaction.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
            }

            // Now run the mount transaction.
            await using ((await daemon.TransactionalDatabase.BeginTransactionAsync(
                new AddMountTransactionRequest
                {
                    MountId = context.MountId,
                    MountRequest = request.MountRequest,
                    MountTypeDebugValue = $"package={request.Tag}",
                    IsBeingMountedOnStartup = context.IsBeingMountedOnStartup,
                    MountAsync = async (cancellationToken) =>
                    {
                        // Figure out the mounter to use.
                        IPackageMounter? selectedMounter = _packageMounterDetector.CreateMounterForPackage(result.PackagePath.FullName);
                        if (selectedMounter == null)
                        {
                            throw new RpcException(new Status(StatusCode.InvalidArgument, $"Unable to detect the type of package located at '{result.PackagePath.FullName}'. Make sure the file is a valid package."));
                        }

                        Directory.CreateDirectory(request.MountRequest.MountPath);

                        var writeStoragePath = _writeScratchPath.ComputeWriteScratchPath(
                            request.MountRequest,
                            result.PackagePath.FullName);
                        _logger.LogInformation($"Mounting {result.PackagePath.FullName} at path on host: {request.MountRequest.MountPath}");
                        _logger.LogInformation($"Using write storage path at: {writeStoragePath}");

                        try
                        {
                            await selectedMounter.MountAsync(
                                result.PackagePath.FullName,
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
                                result.PackagePath.FullName,
                                request.MountRequest.MountPath,
                                request.Tag,
                                selectedMounter),
                            persistentMount: new DaemonDatabasePersistentMount
                            {
                                TagHint = request.Tag,
                                PackagePath = result.PackagePath.FullName,
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
