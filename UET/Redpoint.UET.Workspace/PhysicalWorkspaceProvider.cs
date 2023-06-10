namespace Redpoint.UET.Workspace
{
    using Grpc.Core.Logging;
    using Microsoft.Extensions.Logging;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.UET.Core;
    using Redpoint.UET.Workspace.Descriptors;
    using Redpoint.UET.Workspace.Instance;
    using Redpoint.UET.Workspace.ParallelCopy;
    using Redpoint.UET.Workspace.PhysicalGit;
    using Redpoint.UET.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Resources;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PhysicalWorkspaceProvider : IPhysicalWorkspaceProvider
    {
        private readonly ILogger<PhysicalWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly IParallelCopy _parallelCopy;
        private readonly IVirtualWorkspaceProvider _virtualWorkspaceProvider;
        private readonly IPhysicalGitCheckout _physicalGitCheckout;

        public PhysicalWorkspaceProvider(
            ILogger<PhysicalWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            IParallelCopy parallelCopy,
            IVirtualWorkspaceProvider virtualWorkspaceProvider,
            IPhysicalGitCheckout physicalGitCheckout)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _parallelCopy = parallelCopy;
            _virtualWorkspaceProvider = virtualWorkspaceProvider;
            _physicalGitCheckout = physicalGitCheckout;
        }

        public bool ProvidesFastCopyOnWrite => false;

        public async Task<IWorkspace> GetWorkspaceAsync(IWorkspaceDescriptor workspaceDescriptor, CancellationToken cancellationToken)
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
                    return await _virtualWorkspaceProvider.GetWorkspaceAsync(descriptor, cancellationToken);
                default:
                    throw new NotSupportedException();
            }
        }

        private async Task<IWorkspace> AllocateSnapshotAsync(FolderSnapshotWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync("PhysicalSnapshot", new[] { descriptor.SourcePath.ToLowerInvariant() }.Concat(descriptor.WorkspaceDisambiguators).ToArray());
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
                    cancellationToken);
                usingReservation = true;
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

        private async Task<IWorkspace> AllocateTemporaryAsync(TemporaryWorkspaceDescriptor descriptor, CancellationToken cancellationToken)
        {
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync("PhysicalTemp", descriptor.Name);
            try
            {
                _logger.LogInformation($"Creating physical temporary workspace: {reservation.ReservedPath}");
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
            var usingReservation = false;
            var reservation = await _reservationManager.ReserveAsync("PhysicalGit", new[] { descriptor.RepositoryUrl, descriptor.RepositoryCommitOrRef });
            try
            {
                _logger.LogInformation($"Creating physical Git workspace: {reservation.ReservedPath}");
                await _physicalGitCheckout.PrepareGitWorkspaceAsync(reservation, descriptor, cancellationToken);
                usingReservation = true;
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
    }
}
