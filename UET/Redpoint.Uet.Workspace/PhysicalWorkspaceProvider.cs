﻿namespace Redpoint.Uet.Workspace
{
    using Microsoft.Extensions.Logging;
    using Microsoft.Win32.SafeHandles;
    using Redpoint.IO;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Core;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Instance;
    using Redpoint.Uet.Workspace.ParallelCopy;
    using Redpoint.Uet.Workspace.PhysicalGit;
    using Redpoint.Uet.Workspace.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal class PhysicalWorkspaceProvider : IPhysicalWorkspaceProvider
    {
        private readonly ILogger<PhysicalWorkspaceProvider> _logger;
        private readonly IReservationManagerForUet _reservationManager;
        private readonly IParallelCopy _parallelCopy;
        private readonly IVirtualWorkspaceProvider _virtualWorkspaceProvider;
        private readonly IPhysicalGitCheckout _physicalGitCheckout;
        private readonly IWorkspaceReservationParameterGenerator _parameterGenerator;
        private readonly IProcessExecutor _processExecutor;

        public PhysicalWorkspaceProvider(
            ILogger<PhysicalWorkspaceProvider> logger,
            IReservationManagerForUet reservationManager,
            IParallelCopy parallelCopy,
            IVirtualWorkspaceProvider virtualWorkspaceProvider,
            IPhysicalGitCheckout physicalGitCheckout,
            IWorkspaceReservationParameterGenerator parameterGenerator,
            IProcessExecutor processExecutor)
        {
            _logger = logger;
            _reservationManager = reservationManager;
            _parallelCopy = parallelCopy;
            _virtualWorkspaceProvider = virtualWorkspaceProvider;
            _physicalGitCheckout = physicalGitCheckout;
            _parameterGenerator = parameterGenerator;
            _processExecutor = processExecutor;
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
    }
}
