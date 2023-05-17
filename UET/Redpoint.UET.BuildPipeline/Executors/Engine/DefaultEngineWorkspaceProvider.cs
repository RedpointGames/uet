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

        public async Task<IWorkspace> GetEngineWorkspace(BuildEngineSpecification buildEngineSpecification, string workspaceSuffix, CancellationToken cancellationToken)
        {
            if (buildEngineSpecification._enginePath != null)
            {
                return await _workspaceProvider.GetFolderWorkspaceAsync(buildEngineSpecification._enginePath);
            }
            else if (buildEngineSpecification._uefsPackageTag != null)
            {
                return await _workspaceProvider.GetPackageWorkspaceAsync(buildEngineSpecification._uefsPackageTag, workspaceSuffix, cancellationToken);
            }
            else if (buildEngineSpecification._uefsGitCommit != null)
            {
                return await _workspaceProvider.GetGitWorkspaceAsync(
                    buildEngineSpecification._uefsGitUrl!,
                    buildEngineSpecification._uefsGitCommit,
                    buildEngineSpecification._uefsGitFolders!,
                    workspaceSuffix,
                    cancellationToken);
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
