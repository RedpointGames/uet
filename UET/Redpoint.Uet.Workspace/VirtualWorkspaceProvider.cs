namespace Redpoint.Uet.Workspace
{
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Instance;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Workspace.Credential;
    using Redpoint.Uet.Workspace.Reservation;
    using System.Linq;
    using Grpc.Core;
    using System.Net.Sockets;
    using Redpoint.ProgressMonitor;
    using static Redpoint.Uefs.Protocol.Uefs;
    using Redpoint.Uefs.Protocol;
    using Redpoint.GrpcPipes;

    internal class VirtualWorkspaceProvider : IVirtualWorkspaceProvider
    {
        private readonly ILogger<VirtualWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly UefsClient _uefsClient;
        private readonly ICredentialManager _credentialManager;
        private readonly IRetryableGrpc _retryableGrpc;
        private readonly IMonitorFactory _monitorFactory;

        public VirtualWorkspaceProvider(
            ILogger<VirtualWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            UefsClient uefsClient,
            ICredentialManager credentialManager,
            IRetryableGrpc retryableGrpc,
            IMonitorFactory monitorFactory)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _uefsClient = uefsClient;
            _credentialManager = credentialManager;
            _retryableGrpc = retryableGrpc;
            _monitorFactory = monitorFactory;
        }

        public bool ProvidesFastCopyOnWrite => true;

        private static bool IsUefsUnavailableException(RpcException ex)
        {
            if (ex.StatusCode != StatusCode.Unavailable)
            {
                return false;
            }
            switch (ex.InnerException)
            {
                case HttpRequestException hre:
                    switch (hre.InnerException)
                    {
                        case SocketException se:
                            return se.SocketErrorCode == SocketError.ConnectionRefused;
                    }
                    return false;
            }
            return false;
        }

        public async Task<IWorkspace> GetWorkspaceAsync(IWorkspaceDescriptor workspaceDescriptor, CancellationToken cancellationToken)
        {
            try
            {
                switch (workspaceDescriptor)
                {
                    case FolderAliasWorkspaceDescriptor descriptor:
                        return new LocalWorkspace(descriptor.AliasedPath);
                    case FolderSnapshotWorkspaceDescriptor descriptor:
                        return await AllocateSnapshotAsync(descriptor, cancellationToken);
                    case TemporaryWorkspaceDescriptor descriptor:
                        return await AllocateTemporaryAsync(descriptor, cancellationToken);
                    case GitWorkspaceDescriptor descriptor:
                        return await AllocateGitAsync(descriptor, cancellationToken);
                    case UefsPackageWorkspaceDescriptor descriptor:
                        return await AllocateUefsPackageAsync(descriptor, cancellationToken);
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (RpcException ex) when (IsUefsUnavailableException(ex))
            {
                throw new UefsServiceNotRunningException();
            }
        }


        private async Task<Mount?> GetExistingMountAsync(string mountPath, CancellationToken cancellationToken)
        {
            var response = await _retryableGrpc.RetryableGrpcAsync(
                _uefsClient.ListAsync,
                new ListRequest(),
                new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromSeconds(60) },
                cancellationToken);
            return response.Mounts.FirstOrDefault(x => x.MountPath.Equals(mountPath, StringComparison.InvariantCultureIgnoreCase));
        }

        private async Task<string> MountAsync<TRequest>(
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<MountResponse>> call,
            TRequest request,
            CancellationToken cancellationToken)
        {
            var operation = new ObservableMountOperation<TRequest>(
                _retryableGrpc,
                _monitorFactory,
                call,
                request,
                TimeSpan.FromSeconds(60),
                cancellationToken);
            return await operation.RunAndWaitForMountIdAsync();
        }

        private async Task<IWorkspace> AllocateSnapshotAsync(FolderSnapshotWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var pathKey = descriptor.SourcePath;
            if (OperatingSystem.IsWindows())
            {
                pathKey = pathKey.ToLowerInvariant();
            }
            var parameters = new[] { pathKey }.Concat(descriptor.WorkspaceDisambiguators).ToArray();

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("VirtualSnapshotMount", parameters);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualSnapshotScratch", parameters);
                try
                {
                    var existingMount = await GetExistingMountAsync(mountReservation.ReservedPath, cancellationToken);
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing virtual snapshot workspace using UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UefsWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing virtual snapshot workspace from UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating virtual snapshot workspace using UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                        var mountId = await MountAsync(
                            _uefsClient.MountFolderSnapshot,
                            new MountFolderSnapshotRequest()
                            {
                                MountRequest = new MountRequest
                                {
                                    MountPath = mountReservation.ReservedPath,
                                    TrackPid = GetTrackedPid(),
                                    WriteScratchPath = scratchReservation.ReservedPath,
                                    WriteScratchPersistence = WriteScratchPersistence.Keep,
                                    StartupBehaviour = StartupBehaviour.None,
                                },
                                SourcePath = descriptor.SourcePath,
                            },
                            cancellationToken);
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UefsWorkspace(
                            _uefsClient,
                            mountId,
                            mountReservation.ReservedPath,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing virtual snapshot workspace from UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                    }
                }
                finally
                {
                    if (!usingScratchReservation)
                    {
                        await scratchReservation.DisposeAsync();
                    }
                }
            }
            finally
            {
                if (!usingMountReservation)
                {
                    await mountReservation.DisposeAsync();
                }
            }
        }

        private static int GetTrackedPid()
        {
            if (Environment.GetEnvironmentVariable("UET_UEFS_SKIP_UNMOUNT") == "1")
            {
                return 0;
            }
            return Process.GetCurrentProcess().Id;
        }

        private async Task<IWorkspace> AllocateTemporaryAsync(TemporaryWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync("VirtualTemp", descriptor.Name);
            try
            {
                _logger.LogInformation($"Creating temporary workspace: {reservation.ReservedPath}");
                return new ReservationWorkspace(reservation);
            }
            finally
            {
                if (!usingReservation)
                {
                    await reservation.DisposeAsync();
                }
            }
        }

        private async Task<IWorkspace> AllocateGitAsync(GitWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var normalizedRepositoryUrl = descriptor.RepositoryUrl;
            if (normalizedRepositoryUrl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) ||
                normalizedRepositoryUrl.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase))
            {
                var builder = new UriBuilder(normalizedRepositoryUrl);
                builder.UserName = string.Empty;
                builder.Password = string.Empty;
                normalizedRepositoryUrl = builder.Uri.ToString();
            }

            var parameters = new string[] { normalizedRepositoryUrl, string.Join("/", descriptor.AdditionalFolderLayers), descriptor.RepositoryCommitOrRef };

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("VirtualGitMount", parameters);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualGitScratch", parameters);
                try
                {
                    var existingMount = await GetExistingMountAsync(mountReservation.ReservedPath, cancellationToken);
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing existing virtual Git workspace using UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UefsWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing Git workspace from UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating virtual Git workspace using UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                        var mountId = await MountAsync(
                            _uefsClient.MountGitCommit,
                            new MountGitCommitRequest()
                            {
                                MountRequest = new MountRequest
                                {
                                    MountPath = mountReservation.ReservedPath,
                                    TrackPid = Process.GetCurrentProcess().Id,
                                    WriteScratchPath = scratchReservation.ReservedPath,
                                    WriteScratchPersistence = WriteScratchPersistence.Keep,
                                    StartupBehaviour = StartupBehaviour.None,
                                },
                                Url = descriptor.RepositoryUrl,
                                Commit = descriptor.RepositoryCommitOrRef,
                                Credential = _credentialManager.GetGitCredentialForRepositoryUrl(descriptor.RepositoryUrl),
                            },
                            cancellationToken);
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UefsWorkspace(
                            _uefsClient,
                            mountId,
                            mountReservation.ReservedPath,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing virtual Git workspace from UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                    }
                }
                finally
                {
                    if (!usingScratchReservation)
                    {
                        await scratchReservation.DisposeAsync();
                    }
                }
            }
            finally
            {
                if (!usingMountReservation)
                {
                    await mountReservation.DisposeAsync();
                }
            }
        }

        private async Task<IWorkspace> AllocateUefsPackageAsync(UefsPackageWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var parameters = new string[] { descriptor.PackageTag }.Concat(descriptor.WorkspaceDisambiguators).ToArray();

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("VirtualPackageMount", parameters);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualPackageScratch", parameters);
                try
                {
                    var existingMount = await GetExistingMountAsync(mountReservation.ReservedPath, cancellationToken);
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing existing virtual package workspace using UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UefsWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing virtual package workspace from UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating virtual package workspace using UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                        var mountId = await MountAsync(
                            _uefsClient.MountPackageTag,
                            new MountPackageTagRequest()
                            {
                                MountRequest = new MountRequest
                                {
                                    MountPath = mountReservation.ReservedPath,
                                    WriteScratchPath = scratchReservation.ReservedPath,
                                    WriteScratchPersistence = WriteScratchPersistence.Keep,
                                    StartupBehaviour = StartupBehaviour.None,
                                    TrackPid = Process.GetCurrentProcess().Id,
                                },
                                Tag = descriptor.PackageTag,
                                Credential = _credentialManager.GetRegistryCredentialForTag(descriptor.PackageTag),
                            },
                            cancellationToken);
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UefsWorkspace(
                            _uefsClient,
                            mountId,
                            mountReservation.ReservedPath,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing virtual package workspace from UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                    }
                }
                finally
                {
                    if (!usingScratchReservation)
                    {
                        await scratchReservation.DisposeAsync();
                    }
                }
            }
            finally
            {
                if (!usingMountReservation)
                {
                    await mountReservation.DisposeAsync();
                }
            }
        }
    }
}
