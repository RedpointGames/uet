namespace Redpoint.Uet.Workspace
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.CredentialDiscovery;
    using Redpoint.GrpcPipes;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.ProgressMonitor;
    using Redpoint.Reservation;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Instance;
    using Redpoint.Uet.Workspace.ParallelCopy;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using Redpoint.Uet.Workspace.RemoteZfs;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;
    using static Redpoint.Uefs.Protocol.Uefs;
    using static Redpoint.Uet.Workspace.RemoteZfs.RemoteZfs;

    internal class DefaultWorkspaceProvider : IWorkspaceProvider
    {
        private readonly ILogger<DefaultWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly IPhysicalGitCheckout _physicalGitCheckout;
        private readonly IWorkspaceReservationParameterGenerator _parameterGenerator;
        private readonly IProcessExecutor _processExecutor;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly IRetryableGrpc _retryableGrpc;
        private readonly ICredentialDiscovery _credentialDiscovery;
        private readonly IMonitorFactory _monitorFactory;
        private readonly UefsClient _uefsClient;

        public DefaultWorkspaceProvider(
            ILogger<DefaultWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            IPhysicalGitCheckout physicalGitCheckout,
            IWorkspaceReservationParameterGenerator parameterGenerator,
            IProcessExecutor processExecutor,
            IGrpcPipeFactory grpcPipeFactory,
            IRetryableGrpc retryableGrpc,
            ICredentialDiscovery credentialDiscovery,
            IMonitorFactory monitorFactory,
            UefsClient uefsClient)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _physicalGitCheckout = physicalGitCheckout;
            _parameterGenerator = parameterGenerator;
            _processExecutor = processExecutor;
            _grpcPipeFactory = grpcPipeFactory;
            _retryableGrpc = retryableGrpc;
            _credentialDiscovery = credentialDiscovery;
            _monitorFactory = monitorFactory;
            _uefsClient = uefsClient;
        }

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
                    case TemporaryWorkspaceDescriptor descriptor:
                        return await AllocateTemporaryAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case GitWorkspaceDescriptor descriptor:
                        return await AllocateGitAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case UefsPackageWorkspaceDescriptor descriptor:
                        return await AllocateUefsPackageAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case SharedEngineSourceWorkspaceDescriptor descriptor:
                        return await AllocateSharedEngineSourceAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    case RemoteZfsWorkspaceDescriptor descriptor:
                        return await AllocateRemoteZfsAsync(descriptor, cancellationToken).ConfigureAwait(false);
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (RpcException ex) when (IsUefsUnavailableException(ex))
            {
                throw new UefsServiceNotRunningException();
            }
        }

        private async Task<IWorkspace> AllocateTemporaryAsync(TemporaryWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync(
                "PhysicalTemp",
                _parameterGenerator.ConstructReservationParameters(descriptor.Name)).ConfigureAwait(false);
            try
            {
                _logger.LogInformation($"Creating physical temporary workspace: {reservation.ReservedPath}");
                usingReservation = true;
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
            // Parse the repository URL and get just the components that make it unique.
            string repositoryUniqueUrl;
            try
            {
                var repositoryUri = new Uri(descriptor.RepositoryUrl);
                repositoryUniqueUrl = $"{repositoryUri.Host}{repositoryUri.AbsolutePath}";
                _logger.LogInformation($"Using '{repositoryUniqueUrl}' as unique repository identifier for workspace.");
            }
            catch
            {
                _logger.LogWarning("Unable to parse Git repository URL to reduce uniqueness. More workspaces may be created than necessary to support this build.");
                repositoryUniqueUrl = descriptor.RepositoryUrl;
            }

            var usingReservation = false;
            IReservation? reservation;
            if (descriptor.QueryString?["concurrent"] == "false")
            {
                _logger.LogInformation("Reserving exact workspace as this Git workspace descriptor has concurrent=false...");

                bool? hold = null;
                if (descriptor.QueryString?["hold"] == "true")
                {
                    hold = true;
                }
                else if (descriptor.QueryString?["hold"] == "false")
                {
                    hold = false;
                }

                reservation = await _reservationManager.ReserveExactAsync(
                        StabilityHash.GetStabilityHash($"PhysicalGit:{string.Join("-", [repositoryUniqueUrl, descriptor.RepositoryBranchForReservationParameters])}", 14),
                        cancellationToken,
                        hold: hold).ConfigureAwait(false);
            }
            else
            {
                var reservationParameters =
                    Environment.GetEnvironmentVariable("UET_USE_LESS_UNIQUE_RESERVATION_NAMES_FOR_GIT") == "1"
                    ? _parameterGenerator.ConstructReservationParameters(
                        [repositoryUniqueUrl, descriptor.RepositoryBranchForReservationParameters])
                    : _parameterGenerator.ConstructReservationParameters(
                        [repositoryUniqueUrl, descriptor.RepositoryCommitOrRef]);

                reservation = await _reservationManager.ReserveAsync(
                    "PhysicalGit",
                    reservationParameters).ConfigureAwait(false);
            }
            try
            {
                _logger.LogInformation($"Creating or updating physical Git workspace: {reservation.ReservedPath}");
                await _physicalGitCheckout.PrepareGitWorkspaceAsync(reservation.ReservedPath, descriptor, cancellationToken).ConfigureAwait(false);
                usingReservation = true;
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

        private async Task<IWorkspace> AllocateSharedEngineSourceAsync(SharedEngineSourceWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                throw new PlatformNotSupportedException();
            }

            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\System32\net.exe",
                    Arguments = [
                        "use",
                        "U:",
                        "/DELETE",
                        "/Y"
                    ]
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);
            await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = @"C:\Windows\System32\net.exe",
                    Arguments = [
                        "use",
                        "U:",
                        descriptor.NetworkShare
                    ]
                },
                CaptureSpecification.Passthrough,
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Attempting to acquire lock on network share...");
            do
            {
                try
                {
                    var handle = File.OpenHandle(
                        "U:\\uet-ses.lock",
                        FileMode.Create,
                        FileAccess.ReadWrite,
                        FileShare.None,
                        FileOptions.DeleteOnClose);
                    return new HandleWorkspace(@"U:\", handle);
                }
                catch (IOException ex) when (ex.Message.Contains("another process", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Attempt the next reservation.
                    _logger.LogInformation($"Another build job is currently using the network share. Retrying in 15 seconds...");
                    await Task.Delay(15000, cancellationToken).ConfigureAwait(false);
                }
            }
            while (!cancellationToken.IsCancellationRequested);

            throw new OperationCanceledException(cancellationToken);
        }

        private async Task<IWorkspace> AllocateRemoteZfsAsync(RemoteZfsWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(5, 1, 2600))
            {
                throw new PlatformNotSupportedException();
            }

            if (!IPAddress.TryParse(descriptor.Hostname, out var address))
            {
                var addresses = await Dns.GetHostAddressesAsync(descriptor.Hostname, cancellationToken)
                    .ConfigureAwait(false);
                if (addresses.Length != 0)
                {
                    address = addresses[0];
                }
            }
            if (address == null)
            {
                throw new InvalidOperationException($"Unable to resolve '{descriptor.Hostname}' to an IP address.");
            }

            _logger.LogInformation($"Resolved hostname '{descriptor.Hostname}' to address '{address}' for ZFS client.");

            _logger.LogInformation($"Connecting to ZFS snapshot server...");
            var client = _grpcPipeFactory.CreateNetworkClient(
                new IPEndPoint(address!, descriptor.Port),
                invoker => new RemoteZfsClient(invoker));

            _logger.LogInformation($"Acquiring workspace from ZFS snapshot server...");
            var response = client.AcquireWorkspace(new AcquireWorkspaceRequest
            {
                TemplateId = descriptor.TemplateId,
            }, cancellationToken: cancellationToken);

            var usingOuterReservation = false;
            try
            {
                _logger.LogInformation($"Waiting for workspace to be acquired on ZFS snapshot server...");
                var acquired = await response.ResponseStream.MoveNext(cancellationToken).ConfigureAwait(false);
                if (!acquired)
                {
                    throw new InvalidOperationException($"Unable to acquire workspace on ZFS server!");
                }

                // Allocate a workspace to put our symbolic link in.
                _logger.LogInformation($"Allocating physical workspace for symbolic link...");
                var usingInnerReservation = false;
                var reservation = await _reservationManager.ReserveAsync(
                    "RemoteZfs",
                    _parameterGenerator.ConstructReservationParameters(descriptor.TemplateId)).ConfigureAwait(false);
                try
                {
                    var linkFolder = Path.Combine(reservation.ReservedPath, "S");
                    var linkInfo = new DirectoryInfo(linkFolder);
                    if (linkInfo.Exists)
                    {
                        if (linkInfo.LinkTarget != null)
                        {
                            // Just delete the symbolic link.
                            linkInfo.Delete();
                        }
                        else
                        {
                            // Some process wrote to a path under the symbolic link after it was removed and turned it into a real directory. Nuke it.
                            _logger.LogWarning($"Detected '{linkInfo.FullName}' is not a symbolic link; removing recursively!");
                            await DirectoryAsync.DeleteAsync(linkInfo.FullName, true).ConfigureAwait(false);
                        }
                    }

                    var targetPath = string.IsNullOrWhiteSpace(descriptor.Subpath)
                        ? response.ResponseStream.Current.WindowsShareRemotePath
                        : Path.Combine(response.ResponseStream.Current.WindowsShareRemotePath, descriptor.Subpath);
                    Directory.CreateSymbolicLink(
                        linkFolder,
                        targetPath);

                    _logger.LogInformation($"Remote ZFS workspace '{targetPath}' is now available at '{linkFolder}'.");

                    usingInnerReservation = true;
                    usingOuterReservation = true;

                    return new RemoteZfsWorkspace(response, reservation);
                }
                finally
                {
                    if (!usingInnerReservation)
                    {
                        await reservation.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                if (!usingOuterReservation)
                {
                    response.Dispose();
                }
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

        private static int GetTrackedPid()
        {
            if (Environment.GetEnvironmentVariable("UET_UEFS_SKIP_UNMOUNT") == "1")
            {
                return 0;
            }
            return Environment.ProcessId;
        }

        private async Task<IWorkspace> AllocateUefsPackageAsync(UefsPackageWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var parameters = new string[] { descriptor.PackageTag }.Concat(descriptor.WorkspaceDisambiguators).ToArray();

        retryMount:

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
                        if (descriptor.NoWriteScratchReuse)
                        {
                            // We do this here, instead of skipping over GetExistingMountAsync, because we want to force unmounting of
                            // corrupt UEFS mounts rather than leaving them around.
                            mountIsValid = false;
                        }
                        else
                        {
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
                        string? mountId;
                        try
                        {
                            mountId = await MountAsync(
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
                        }
                        catch (RpcException ex) when (ex.StatusCode == StatusCode.Internal && ex.Status.Detail.Contains("ERROR_SHARING_VIOLATION", StringComparison.Ordinal))
                        {
                            _logger.LogInformation("Temporary error in UEFS when mounting package, retrying in 1 second...");
                            await Task.Delay(1000, cancellationToken);
                            goto retryMount;
                        }
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
