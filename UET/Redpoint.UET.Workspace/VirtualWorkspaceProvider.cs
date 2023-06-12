namespace Redpoint.UET.Workspace
{
    using Redpoint.UET.Workspace.Descriptors;
    using Redpoint.UET.Workspace.Instance;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static Uefs.UEFS;
    using Uefs;
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.Workspace.Credential;
    using Redpoint.UET.Workspace.Reservation;
    using System.Linq;
    using Grpc.Core;
    using System.Net.Sockets;

    internal class VirtualWorkspaceProvider : IVirtualWorkspaceProvider
    {
        private readonly ILogger<VirtualWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly UEFSClient _uefsClient;
        private readonly ICredentialManager _credentialManager;

        public VirtualWorkspaceProvider(
            ILogger<VirtualWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            UEFSClient uefsClient,
            ICredentialManager credentialManager)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _uefsClient = uefsClient;
            _credentialManager = credentialManager;
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

        private async Task<string> WaitForMountToComplete(AsyncServerStreamingCall<MountResponse> stream)
        {
            while (await stream.ResponseStream.MoveNext())
            {
                var entry = stream.ResponseStream.Current;
                if (entry.PollingResponse.Complete)
                {
                    if (!string.IsNullOrWhiteSpace(entry.PollingResponse.Err))
                    {
                        throw new RpcException(new Status(StatusCode.Internal, entry.PollingResponse.Err));
                    }
                    return entry.MountId;
                }
            }
            throw new InvalidOperationException();
        }

        private async Task<IWorkspace> AllocateSnapshotAsync(FolderSnapshotWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var pathKey = descriptor.SourcePath;
            if (OperatingSystem.IsWindows())
            {
                pathKey = pathKey.ToLowerInvariant();
            }
            var parameters = new[] { pathKey }.Concat(descriptor.WorkspaceDisambiguators).ToArray();

            var workspaceOptions = descriptor.WorkspaceOptions ?? new VirtualisedWorkspaceOptions();

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("VirtualSnapshotMount", parameters);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualSnapshotScratch", parameters);
                try
                {
                    var existingMount = (await _uefsClient.ListAsync(new ListRequest())).Mounts
                        .FirstOrDefault(x => x.MountPath.Equals(mountReservation.ReservedPath, StringComparison.InvariantCultureIgnoreCase));
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing virtual snapshot workspace using UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UEFSWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            workspaceOptions,
                        new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing virtual snapshot workspace from UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating virtual snapshot workspace using UEFS ({descriptor.SourcePath}: {mountReservation.ReservedPath})");
                        var mountId = await WaitForMountToComplete(_uefsClient.MountFolderSnapshot(new Uefs.MountFolderSnapshotRequest()
                        {
                            MountRequest = new Uefs.MountRequest
                            {
                                MountPath = mountReservation.ReservedPath,
                                PersistMode = Uefs.MountPersistMode.None,
                                TrackPid = workspaceOptions.UnmountAfterUse ? Process.GetCurrentProcess().Id : 0,
                            },
                            SourcePath = descriptor.SourcePath,
                            ScratchPath = scratchReservation.ReservedPath,
                        }));
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UEFSWorkspace(
                            _uefsClient,
                            mountId,
                            mountReservation.ReservedPath,
                            workspaceOptions,
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
            var workspaceOptions = descriptor.WorkspaceOptions ?? new VirtualisedWorkspaceOptions();

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
                    var existingMount = (await _uefsClient.ListAsync(new ListRequest())).Mounts
                        .FirstOrDefault(x => x.MountPath.Equals(mountReservation.ReservedPath, StringComparison.InvariantCultureIgnoreCase));
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing existing virtual Git workspace using UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UEFSWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            workspaceOptions,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing Git workspace from UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating virtual Git workspace using UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                        var mountId = await WaitForMountToComplete(_uefsClient.MountGitCommit(new Uefs.MountGitCommitRequest()
                        {
                            MountRequest = new Uefs.MountRequest
                            {
                                MountPath = mountReservation.ReservedPath,
                                PersistMode = Uefs.MountPersistMode.None,
                                TrackPid = workspaceOptions.UnmountAfterUse ? Process.GetCurrentProcess().Id : 0,
                            },
                            Url = descriptor.RepositoryUrl,
                            Commit = descriptor.RepositoryCommitOrRef,
                            Credential = _credentialManager.GetGitCredentialForRepositoryUrl(descriptor.RepositoryUrl),
                            ScratchPath = scratchReservation.ReservedPath,
                        }));
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UEFSWorkspace(
                            _uefsClient,
                            mountId,
                            mountReservation.ReservedPath,
                            workspaceOptions,
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
            var workspaceOptions = descriptor.WorkspaceOptions ?? new VirtualisedWorkspaceOptions();

            var parameters = new string[] { descriptor.PackageTag }.Concat(descriptor.WorkspaceDisambiguators).ToArray();

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("VirtualPackageMount", parameters);
            try
            {
                var existingMount = (await _uefsClient.ListAsync(new ListRequest())).Mounts
                    .FirstOrDefault(x => x.MountPath.Equals(mountReservation.ReservedPath, StringComparison.InvariantCultureIgnoreCase));
                if (existingMount != null)
                {
                    _logger.LogInformation($"Reusing existing virtual package workspace using UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                    usingMountReservation = true;
                    return new UEFSWorkspace(
                        _uefsClient,
                        existingMount.Id,
                        mountReservation.ReservedPath,
                        workspaceOptions,
                        new[] { mountReservation },
                        _logger,
                        $"Releasing virtual package workspace from UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                }
                else
                {
                    _logger.LogInformation($"Creating virtual package workspace using UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
                    var mountId = await WaitForMountToComplete(_uefsClient.MountPackageTag(new Uefs.MountPackageTagRequest()
                    {
                        MountRequest = new Uefs.MountRequest
                        {
                            MountPath = mountReservation.ReservedPath,
                            PersistMode = Uefs.MountPersistMode.None,
                            TrackPid = workspaceOptions.UnmountAfterUse ? Process.GetCurrentProcess().Id : 0,
                        },
                        Tag = descriptor.PackageTag,
                        Credential = _credentialManager.GetRegistryCredentialForTag(descriptor.PackageTag),
                    }));
                    usingMountReservation = true;
                    return new UEFSWorkspace(
                        _uefsClient,
                        mountId,
                        mountReservation.ReservedPath,
                        workspaceOptions,
                        new[] { mountReservation },
                        _logger,
                        $"Releasing virtual package workspace from UEFS ({descriptor.PackageTag}): {mountReservation.ReservedPath}");
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
