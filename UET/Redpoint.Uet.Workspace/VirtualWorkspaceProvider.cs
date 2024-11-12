﻿namespace Redpoint.Uet.Workspace
{
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Instance;
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Workspace.Reservation;
    using System.Linq;
    using Grpc.Core;
    using System.Net.Sockets;
    using Redpoint.ProgressMonitor;
    using static Redpoint.Uefs.Protocol.Uefs;
    using Redpoint.Uefs.Protocol;
    using Redpoint.GrpcPipes;
    using Redpoint.Reservation;
    using System.IO.Compression;
    using System.IO;
    using Redpoint.CredentialDiscovery;

    internal class VirtualWorkspaceProvider : IVirtualWorkspaceProvider
    {
        private readonly ILogger<VirtualWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly UefsClient _uefsClient;
        private readonly ICredentialDiscovery _credentialDiscovery;
        private readonly IRetryableGrpc _retryableGrpc;
        private readonly IMonitorFactory _monitorFactory;
        private readonly IWorkspaceReservationParameterGenerator _parameterGenerator;

        public VirtualWorkspaceProvider(
            ILogger<VirtualWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            UefsClient uefsClient,
            ICredentialDiscovery credentialDiscovery,
            IRetryableGrpc retryableGrpc,
            IMonitorFactory monitorFactory,
            IWorkspaceReservationParameterGenerator parameterGenerator)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _uefsClient = uefsClient;
            _credentialDiscovery = credentialDiscovery;
            _retryableGrpc = retryableGrpc;
            _monitorFactory = monitorFactory;
            _parameterGenerator = parameterGenerator;
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
                        return await AllocateSnapshotAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case TemporaryWorkspaceDescriptor descriptor:
                        return await AllocateTemporaryAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case GitWorkspaceDescriptor descriptor:
                        return await AllocateGitAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case UefsPackageWorkspaceDescriptor descriptor:
                        return await AllocateUefsPackageAsync(descriptor, cancellationToken).ConfigureAwait(false);
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
                new GrpcRetryConfiguration { RequestTimeout = TimeSpan.FromMinutes(60) },
                cancellationToken).ConfigureAwait(false);
            return response.Mounts.FirstOrDefault(x => x.MountPath.Equals(mountPath, StringComparison.OrdinalIgnoreCase));
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
                TimeSpan.FromMinutes(60),
                cancellationToken);
            return await operation.RunAndWaitForMountIdAsync().ConfigureAwait(false);
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
            var mountReservation = await _reservationManager.ReserveAsync("VirtualSnapshotMount", parameters).ConfigureAwait(false);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualSnapshotScratch", parameters).ConfigureAwait(false);
                try
                {
                    var existingMount = await GetExistingMountAsync(mountReservation.ReservedPath, cancellationToken).ConfigureAwait(false);
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
                            cancellationToken).ConfigureAwait(false);
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
                        await scratchReservation.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (!usingMountReservation)
                {
                    await mountReservation.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private static int GetTrackedPid()
        {
            if (Environment.GetEnvironmentVariable("UET_UEFS_SKIP_UNMOUNT") == "1")
            {
                return 0;
            }
            return Environment.ProcessId;
        }

        private async Task<IWorkspace> AllocateTemporaryAsync(TemporaryWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            // @note: We use the same "PhysicalTemp" name here because there's no issue sharing
            // temporary workspaces with the physical provider.
            var reservation = await _reservationManager.ReserveAsync("PhysicalTemp", descriptor.Name).ConfigureAwait(false);
            try
            {
                _logger.LogInformation($"Creating temporary workspace: {reservation.ReservedPath}");
                return new ReservationWorkspace(reservation);
            }
            finally
            {
                if (!usingReservation)
                {
                    await reservation.DisposeAsync().ConfigureAwait(false);
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
            var mountReservation = await _reservationManager.ReserveAsync("VirtualGitMount", parameters).ConfigureAwait(false);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualGitScratch", parameters).ConfigureAwait(false);
                try
                {
                    var existingMount = await GetExistingMountAsync(mountReservation.ReservedPath, cancellationToken).ConfigureAwait(false);
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
                        var list = new List<IReservation>();
                        var unwind = true;
                        try
                        {
                            foreach (var consoleZip in descriptor.AdditionalFolderZips)
                            {
                                var reservation = await _reservationManager.ReserveAsync(
                                    "ConsoleZip",
                                    consoleZip).ConfigureAwait(false);
                                list.Add(reservation);

                                var extractPath = Path.Combine(reservation.ReservedPath, "extracted");
                                Directory.CreateDirectory(extractPath);
                                if (!File.Exists(Path.Combine(reservation.ReservedPath, ".console-zip-extracted")))
                                {
                                    _logger.LogInformation($"Extracting '{consoleZip}' to '{extractPath}'...");
                                    using (var stream = new FileStream(consoleZip, FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        var archive = new ZipArchive(stream);
                                        archive.ExtractToDirectory(extractPath);
                                    }
                                    File.WriteAllText(Path.Combine(reservation.ReservedPath, ".console-zip-extracted"), "done");
                                }
                            }

                            _logger.LogInformation($"Creating virtual Git workspace using UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                            var mountRequest = new MountGitCommitRequest()
                            {
                                MountRequest = new MountRequest
                                {
                                    MountPath = mountReservation.ReservedPath,
                                    TrackPid = GetTrackedPid(),
                                    WriteScratchPath = scratchReservation.ReservedPath,
                                    WriteScratchPersistence = WriteScratchPersistence.Keep,
                                    StartupBehaviour = StartupBehaviour.None,
                                },
                                Url = descriptor.RepositoryUrl,
                                Commit = descriptor.RepositoryCommitOrRef,
                                Credential = _credentialDiscovery.GetGitCredential(descriptor.RepositoryUrl),
                            };
                            mountRequest.FolderLayers.AddRange(list.Select(x => x.ReservedPath));
                            var mountId = await MountAsync(
                                _uefsClient.MountGitCommit,
                                mountRequest,
                                cancellationToken).ConfigureAwait(false);
                            usingMountReservation = true;
                            usingScratchReservation = true;
                            var workspace = new UefsWorkspace(
                                _uefsClient,
                                mountId,
                                mountReservation.ReservedPath,
                                new[] { mountReservation, scratchReservation }.Concat(list).ToArray(),
                                _logger,
                                $"Releasing virtual Git workspace from UEFS ({normalizedRepositoryUrl}, {descriptor.RepositoryCommitOrRef}): {mountReservation.ReservedPath}");
                            unwind = false;
                            return workspace;
                        }
                        finally
                        {
                            if (unwind)
                            {
                                foreach (var l in list)
                                {
                                    await l.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (!usingScratchReservation)
                    {
                        await scratchReservation.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (!usingMountReservation)
                {
                    await mountReservation.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private async Task<IWorkspace> AllocateUefsPackageAsync(UefsPackageWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var parameters = new string[] { descriptor.PackageTag }.Concat(descriptor.WorkspaceDisambiguators).ToArray();

            var usingMountReservation = false;
            var mountReservation = await _reservationManager.ReserveAsync("VirtualPackageMount", _parameterGenerator.ConstructReservationParameters(parameters)).ConfigureAwait(false);
            try
            {
                var usingScratchReservation = false;
                var scratchReservation = await _reservationManager.ReserveAsync("VirtualPackageScratch", _parameterGenerator.ConstructReservationParameters(parameters)).ConfigureAwait(false);
                try
                {
                    var existingMount = await GetExistingMountAsync(mountReservation.ReservedPath, cancellationToken).ConfigureAwait(false);
                    if (existingMount != null)
                    {
                        bool mountIsValid = true;
                        try
                        {
                            // When the mount is in a broken state, this is usually the first call that fails later on in UET.
                            // Check that the Programs directory exists now so we can discard and remount if it's not there.
                            Directory.EnumerateFiles(Path.Combine(mountReservation.ReservedPath, "Engine", "Source", "Programs"));
                        }
                        catch (DirectoryNotFoundException)
                        {
                            mountIsValid = false;
                        }
                        catch (IOException)
                        {
                            mountIsValid = false;
                        }

                        if (mountIsValid)
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
                            _logger.LogInformation($"Existing virtual package workspace using UEFS was not valid, unmounting...");
                            await _uefsClient.UnmountAsync(new UnmountRequest
                            {
                                MountId = existingMount.Id,
                            }, deadline: DateTime.UtcNow.AddSeconds(60), cancellationToken: cancellationToken);
                        }
                    }

                    // We can fallthrough to this case if there is no existing mount or if we unmounted it
                    // because it wasn't in a valid state.
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
                                    TrackPid = GetTrackedPid(),
                                },
                                Tag = descriptor.PackageTag,
                                Credential = _credentialDiscovery.GetRegistryCredential(descriptor.PackageTag),
                            },
                            cancellationToken).ConfigureAwait(false);
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
                        await scratchReservation.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (!usingMountReservation)
                {
                    await mountReservation.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }
}
