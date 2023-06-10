namespace Redpoint.UET.BuildPipeline.Executors.Engine
{
    using Redpoint.UET.Workspace;
    using Redpoint.UET.Workspace.Descriptors;
    using System.Threading.Tasks;

    internal class DefaultEngineWorkspaceProvider : IEngineWorkspaceProvider
    {
        private readonly IDynamicWorkspaceProvider _workspaceProvider;

        public DefaultEngineWorkspaceProvider(IDynamicWorkspaceProvider workspaceProvider)
        {
            _workspaceProvider = workspaceProvider;
        }

        public async Task<IWorkspace> GetEngineWorkspace(
            BuildEngineSpecification buildEngineSpecification,
            string workspaceSuffix,
            CancellationToken cancellationToken)
        {
            if (buildEngineSpecification._enginePath != null)
            {
                if (_workspaceProvider.ProvidesFastCopyOnWrite)
                {
                    return await _workspaceProvider.GetWorkspaceAsync(
                        new FolderSnapshotWorkspaceDescriptor
                        {
                            SourcePath = buildEngineSpecification._enginePath,
                            WorkspaceDisambiguators = new[] { workspaceSuffix },
                        },
                        cancellationToken);
                }
                else
                {
                    return await _workspaceProvider.GetWorkspaceAsync(
                        new FolderAliasWorkspaceDescriptor
                        {
                            AliasedPath = buildEngineSpecification._enginePath
                        },
                        cancellationToken);
                }
            }
            else if (buildEngineSpecification._uefsPackageTag != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new UefsPackageWorkspaceDescriptor
                    {
                        PackageTag = buildEngineSpecification._uefsPackageTag,
                        WorkspaceDisambiguators = new[] { workspaceSuffix },
                    },
                    cancellationToken);
            }
            else if (buildEngineSpecification._uefsGitCommit != null)
            {
                return await _workspaceProvider.GetWorkspaceAsync(
                    new GitWorkspaceDescriptor
                    {
                        RepositoryUrl = buildEngineSpecification._uefsGitUrl!,
                        RepositoryCommitOrRef = buildEngineSpecification._uefsGitCommit,
                        AdditionalFolderLayers = buildEngineSpecification._uefsGitFolders!,
                        WorkspaceDisambiguators = new[] { workspaceSuffix },
                        ProjectFolderName = null,
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
