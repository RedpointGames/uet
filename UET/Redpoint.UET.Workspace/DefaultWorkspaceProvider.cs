namespace Redpoint.UET.Workspace
{
    using Grpc.Core;
    using Grpc.Core.Utils;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.Core;
    using Redpoint.UET.Workspace.Credential;
    using Redpoint.UET.Workspace.Reservation;
    using System.Diagnostics;
    using System.IO;
    using System.Threading.Tasks;
    using Uefs;
    using static Uefs.UEFS;

    internal class DefaultWorkspaceProvider : IWorkspaceProvider
    {
        private readonly ILogger<DefaultWorkspaceProvider> _logger;
        private readonly IReservationManager _reservationManager;
        private readonly UEFSClient _uefsClient;
        private readonly ICredentialManager _credentialManager;

        public DefaultWorkspaceProvider(
            ILogger<DefaultWorkspaceProvider> logger,
            IReservationManager reservationManager,
            UEFSClient uefsClient,
            ICredentialManager credentialManager)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _uefsClient = uefsClient;
            _credentialManager = credentialManager;
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

        public async Task<IWorkspace> GetFolderWorkspaceAsync(
            string path,
            string[] disambiguators,
            WorkspaceOptions workspaceOptions)
        {
            var pathKey = path;
            if (OperatingSystem.IsWindows())
            {
                pathKey = pathKey.ToLowerInvariant();
            }
            var parameters = new[] { pathKey }.Concat(disambiguators).ToArray();

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("FolderWorkspaceMount", parameters);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("FolderWorkspaceScratch", parameters);
                try
                {
                    var existingMount = (await _uefsClient.ListAsync(new ListRequest())).Mounts
                        .FirstOrDefault(x => x.MountPath.Equals(mountReservation.ReservedPath, StringComparison.InvariantCultureIgnoreCase));
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing folder workspace using UEFS ({path}: {mountReservation.ReservedPath})");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UEFSWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            workspaceOptions,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing folder workspace from UEFS ({path}: {mountReservation.ReservedPath})");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating folder workspace using UEFS ({path}: {mountReservation.ReservedPath})");
                        var mountId = await WaitForMountToComplete(_uefsClient.MountFolderSnapshot(new Uefs.MountFolderSnapshotRequest()
                        {
                            MountRequest = new Uefs.MountRequest
                            {
                                MountPath = mountReservation.ReservedPath,
                                PersistMode = Uefs.MountPersistMode.None,
                                TrackPid = workspaceOptions.UnmountAfterUse ? Process.GetCurrentProcess().Id : 0,
                            },
                            SourcePath = path,
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
                            $"Releasing folder workspace from UEFS ({path}: {mountReservation.ReservedPath})");
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

        public async Task<IWorkspace> GetTempWorkspaceAsync(string name)
        {
            var usingTempReservation = false;
            var tempReservation = await _reservationManager.ReserveAsync("TempWorkspace", name);
            try
            {
                _logger.LogInformation($"Creating temporary workspace: {tempReservation.ReservedPath}");
                return new LocalWorkspace(tempReservation.ReservedPath);
            }
            finally
            {
                if (!usingTempReservation)
                {
                    await tempReservation.DisposeAsync();
                }
            }
        }

        public async Task<IWorkspace> GetGitWorkspaceAsync(
            string repository,
            string commit,
            string[] folders,
            string workspaceSuffix,
            WorkspaceOptions workspaceOptions,
            CancellationToken cancellationToken)
        {
            var normalizedRepositoryUrl = repository;
            if (normalizedRepositoryUrl.StartsWith("https://", StringComparison.InvariantCultureIgnoreCase) ||
                normalizedRepositoryUrl.StartsWith("http://", StringComparison.InvariantCultureIgnoreCase))
            {
                var builder = new UriBuilder(normalizedRepositoryUrl);
                builder.UserName = string.Empty;
                builder.Password = string.Empty;
                normalizedRepositoryUrl = builder.Uri.ToString();
            }

            var parameters = new string[] { normalizedRepositoryUrl, string.Join("/", folders), commit };

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("GitWorkspaceMount", parameters);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("GitWorkspaceScratch", parameters);
                try
                {
                    var existingMount = (await _uefsClient.ListAsync(new ListRequest())).Mounts
                        .FirstOrDefault(x => x.MountPath.Equals(mountReservation.ReservedPath, StringComparison.InvariantCultureIgnoreCase));
                    if (existingMount != null)
                    {
                        _logger.LogInformation($"Reusing existing Git workspace using UEFS ({normalizedRepositoryUrl}, {commit}): {mountReservation.ReservedPath}");
                        usingMountReservation = true;
                        usingScratchReservation = true;
                        return new UEFSWorkspace(
                            _uefsClient,
                            existingMount.Id,
                            mountReservation.ReservedPath,
                            workspaceOptions,
                            new[] { mountReservation, scratchReservation },
                            _logger,
                            $"Releasing Git workspace from UEFS ({normalizedRepositoryUrl}, {commit}): {mountReservation.ReservedPath}");
                    }
                    else
                    {
                        _logger.LogInformation($"Creating Git workspace using UEFS ({normalizedRepositoryUrl}, {commit}): {mountReservation.ReservedPath}");
                        var mountId = await WaitForMountToComplete(_uefsClient.MountGitCommit(new Uefs.MountGitCommitRequest()
                        {
                            MountRequest = new Uefs.MountRequest
                            {
                                MountPath = mountReservation.ReservedPath,
                                PersistMode = Uefs.MountPersistMode.None,
                                TrackPid = workspaceOptions.UnmountAfterUse ? Process.GetCurrentProcess().Id : 0,
                            },
                            Url = repository,
                            Commit = commit,
                            Credential = _credentialManager.GetGitCredentialForRepositoryUrl(repository),
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
                            $"Releasing Git workspace from UEFS ({normalizedRepositoryUrl}, {commit}): {mountReservation.ReservedPath}");
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

        public async Task<IWorkspace> GetPackageWorkspaceAsync(
            string tag,
            string workspaceSuffix,
            WorkspaceOptions workspaceOptions,
            CancellationToken cancellationToken)
        {
            var parameters = new string[] { tag, workspaceSuffix };

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("PackageWorkspaceMount", parameters);
            try
            {
                var existingMount = (await _uefsClient.ListAsync(new ListRequest())).Mounts
                    .FirstOrDefault(x => x.MountPath.Equals(mountReservation.ReservedPath, StringComparison.InvariantCultureIgnoreCase));
                if (existingMount != null)
                {
                    _logger.LogInformation($"Reusing existing package workspace using UEFS ({tag}): {mountReservation.ReservedPath}");
                    usingMountReservation = true;
                    return new UEFSWorkspace(
                        _uefsClient,
                        existingMount.Id,
                        mountReservation.ReservedPath,
                        workspaceOptions,
                        new[] { mountReservation },
                        _logger,
                        $"Releasing package workspace from UEFS ({tag}): {mountReservation.ReservedPath}");
                }
                else
                {
                    _logger.LogInformation($"Creating package workspace using UEFS ({tag}): {mountReservation.ReservedPath}");
                    var mountId = await WaitForMountToComplete(_uefsClient.MountPackageTag(new Uefs.MountPackageTagRequest()
                    {
                        MountRequest = new Uefs.MountRequest
                        {
                            MountPath = mountReservation.ReservedPath,
                            PersistMode = Uefs.MountPersistMode.None,
                            TrackPid = workspaceOptions.UnmountAfterUse ? Process.GetCurrentProcess().Id : 0,
                        },
                        Tag = tag,
                        Credential = _credentialManager.GetRegistryCredentialForTag(tag),
                    }));
                    usingMountReservation = true;
                    return new UEFSWorkspace(
                        _uefsClient,
                        mountId,
                        mountReservation.ReservedPath,
                        workspaceOptions,
                        new[] { mountReservation },
                        _logger,
                        $"Releasing package workspace from UEFS ({tag}): {mountReservation.ReservedPath}");
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
