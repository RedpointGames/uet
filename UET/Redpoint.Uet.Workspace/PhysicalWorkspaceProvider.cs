namespace Redpoint.Uet.Workspace
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.GrpcPipes;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
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
    using System.Threading;
    using System.Threading.Tasks;
    using static Redpoint.Uet.Workspace.RemoteZfs.RemoteZfs;

    internal class PhysicalWorkspaceProvider : IPhysicalWorkspaceProvider
    {
        private readonly ILogger<PhysicalWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly IParallelCopy _parallelCopy;
        private readonly IVirtualWorkspaceProvider _virtualWorkspaceProvider;
        private readonly IPhysicalGitCheckout _physicalGitCheckout;
        private readonly IWorkspaceReservationParameterGenerator _parameterGenerator;
        private readonly IProcessExecutor _processExecutor;
        private readonly IGrpcPipeFactory _grpcPipeFactory;

        public PhysicalWorkspaceProvider(
            ILogger<PhysicalWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            IParallelCopy parallelCopy,
            IVirtualWorkspaceProvider virtualWorkspaceProvider,
            IPhysicalGitCheckout physicalGitCheckout,
            IWorkspaceReservationParameterGenerator parameterGenerator,
            IProcessExecutor processExecutor,
            IGrpcPipeFactory grpcPipeFactory)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _parallelCopy = parallelCopy;
            _virtualWorkspaceProvider = virtualWorkspaceProvider;
            _physicalGitCheckout = physicalGitCheckout;
            _parameterGenerator = parameterGenerator;
            _processExecutor = processExecutor;
            _grpcPipeFactory = grpcPipeFactory;
        }

        public bool ProvidesFastCopyOnWrite => false;

        public async Task<IWorkspace> GetWorkspaceAsync(IWorkspaceDescriptor workspaceDescriptor, CancellationToken cancellationToken)
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
                    return await _virtualWorkspaceProvider.GetWorkspaceAsync(descriptor, cancellationToken).ConfigureAwait(false);
                case SharedEngineSourceWorkspaceDescriptor descriptor:
                    return await AllocateSharedEngineSourceAsync(descriptor, cancellationToken).ConfigureAwait(false);
                case RemoteZfsWorkspaceDescriptor descriptor:
                    return await AllocateRemoteZfsAsync(descriptor, cancellationToken).ConfigureAwait(false);
                default:
                    throw new NotSupportedException();
            }
        }

        private async Task<IWorkspace> AllocateSnapshotAsync(FolderSnapshotWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync(
                "PhysicalSnapshot",
                _parameterGenerator.ConstructReservationParameters(
                    new[] { descriptor.SourcePath.ToLowerInvariant() }
                    .Concat(descriptor.WorkspaceDisambiguators))).ConfigureAwait(false);
            try
            {
                _logger.LogInformation($"Creating physical snapshot workspace: {reservation.ReservedPath} (of {descriptor.SourcePath})");
                await _parallelCopy.CopyAsync(
                    new CopyDescriptor
                    {
                        SourcePath = descriptor.SourcePath,
                        DestinationPath = reservation.ReservedPath,
                        DirectoriesToRemoveExtraFilesUnder = new HashSet<string>
                        {
                            "Source",
                            "Content",
                            "Resources",
                            "Config"
                        },
                        ExcludePaths = new HashSet<string>
                        {
                            ".uet",
                            ".git",
                            "Engine/Saved/BuildGraph",
                        }
                    },
                    cancellationToken).ConfigureAwait(false);
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

        private async Task<IWorkspace> AllocateTemporaryAsync(TemporaryWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync(
                "PhysicalTemp",
                _parameterGenerator.ConstructReservationParameters(descriptor.Name)).ConfigureAwait(false);
            try
            {
                _logger.LogInformation($"Creating physical temporary workspace: {reservation.ReservedPath}");
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
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync(
                "PhysicalGit",
                _parameterGenerator.ConstructReservationParameters([descriptor.RepositoryUrl, descriptor.RepositoryCommitOrRef])).ConfigureAwait(false);
            try
            {
                _logger.LogInformation($"Creating physical Git workspace: {reservation.ReservedPath}");
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
    }
}
