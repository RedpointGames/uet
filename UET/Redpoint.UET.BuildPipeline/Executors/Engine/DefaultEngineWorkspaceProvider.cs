namespace Redpoint.UET.BuildPipeline.Executors.Engine
{
    using Redpoint.UET.Workspace;
    using System.Threading.Tasks;

    internal class DefaultEngineWorkspaceProvider : IEngineWorkspaceProvider
    {
        private readonly IWorkspaceProvider _workspaceProvider;

        public DefaultEngineWorkspaceProvider(IWorkspaceProvider workspaceProvider)
        {
            _workspaceProvider = workspaceProvider;
        }

        public async Task<IWorkspace> GetEngineWorkspace(
            BuildEngineSpecification buildEngineSpecification,
            string workspaceSuffix,
            bool useStorageVirtualisation,
            CancellationToken cancellationToken)
        {
            if (buildEngineSpecification._enginePath != null)
            {
                if (useStorageVirtualisation)
                {
                    return await _workspaceProvider.GetFolderWorkspaceAsync(
                        buildEngineSpecification._enginePath,
                        new[] { workspaceSuffix },
                        new WorkspaceOptions { UnmountAfterUse = false });
                }
                else
                {
                    return await _workspaceProvider.GetExistingPathAsWorkspaceAsync(buildEngineSpecification._enginePath);
                }
            }
            else if (buildEngineSpecification._uefsPackageTag != null)
            {
                return await _workspaceProvider.GetPackageWorkspaceAsync(
                    buildEngineSpecification._uefsPackageTag,
                    workspaceSuffix,
                    new WorkspaceOptions
                    {
                        UnmountAfterUse = false,
                    },
                    cancellationToken);
            }
            else if (buildEngineSpecification._uefsGitCommit != null)
            {
                return await _workspaceProvider.GetGitWorkspaceAsync(
                    buildEngineSpecification._uefsGitUrl!,
                    buildEngineSpecification._uefsGitCommit,
                    buildEngineSpecification._uefsGitFolders!,
                    workspaceSuffix,
                    new WorkspaceOptions
                    {
                        UnmountAfterUse = false,
                    },
                    cancellationToken);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
