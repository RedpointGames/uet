namespace Redpoint.Uet.BuildPipeline.Executors.Engine
{
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using Redpoint.Uet.Workspace.Reservation;
    using System.Threading.Tasks;

    internal class DefaultEngineWorkspaceProvider : IEngineWorkspaceProvider
    {
        private readonly IWorkspaceProvider _workspaceProvider;
        private readonly IReservationManagerForUet _reservationManagerForUet;

        public DefaultEngineWorkspaceProvider(
            IWorkspaceProvider workspaceProvider,
            IReservationManagerForUet reservationManagerForUet)
        {
            _workspaceProvider = workspaceProvider;
            _reservationManagerForUet = reservationManagerForUet;
        }

        public async Task<IWorkspace> GetEngineWorkspace(
            BuildEngineSpecification buildEngineSpecification,
            string workspaceSuffix,
            CancellationToken cancellationToken)
        {
            if (buildEngineSpecification._enginePath != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new FolderAliasWorkspaceDescriptor
                    {
                        AliasedPath = buildEngineSpecification._enginePath
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else if (buildEngineSpecification._uefsPackageTag != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new UefsPackageWorkspaceDescriptor
                    {
                        PackageTag = buildEngineSpecification._uefsPackageTag,
                        WorkspaceDisambiguators = new[] { workspaceSuffix },
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else if (buildEngineSpecification._sesNetworkShare != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new SharedEngineSourceWorkspaceDescriptor
                    {
                        NetworkShare = buildEngineSpecification._sesNetworkShare,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else if (buildEngineSpecification._remoteZfs != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    RemoteZfsWorkspaceDescriptor.Parse(buildEngineSpecification._remoteZfs),
                    cancellationToken).ConfigureAwait(false);
            }
            else if (buildEngineSpecification._gitCommit != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new GitWorkspaceDescriptor
                    {
                        RepositoryUrl = buildEngineSpecification._gitUrl!,
                        RepositoryCommitOrRef = buildEngineSpecification._gitCommit,
                        AdditionalFolderLayers = Array.Empty<string>(),
                        AdditionalFolderZips = buildEngineSpecification._gitConsoleZips!,
                        WorkspaceDisambiguators = new[] { workspaceSuffix },
                        ProjectFolderName = null,
                        BuildType = buildEngineSpecification.EngineBuildType != BuildEngineSpecificationEngineBuildType.None
                            ? GitWorkspaceDescriptorBuildType.Engine
                            : GitWorkspaceDescriptorBuildType.Generic,
                        WindowsSharedGitCachePath = buildEngineSpecification._gitSharedWindowsCachePath,
                        MacSharedGitCachePath = buildEngineSpecification._gitSharedMacCachePath,
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
