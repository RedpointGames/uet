namespace Redpoint.Uet.BuildPipeline.MultiWorkspace
{
    using Redpoint.Uet.Workspace;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class DefaultMultiWorkspaceAllocator : IMultiWorkspaceAllocator
    {
        private readonly IDynamicWorkspaceProvider _workspaceProvider;

        public DefaultMultiWorkspaceAllocator(
            IDynamicWorkspaceProvider workspaceProvider)
        {
            _workspaceProvider = workspaceProvider;
        }

        public async Task<MultiWorkspace> AllocateAsync(
            IReadOnlyDictionary<string, MultiWorkspaceDescriptor> descriptors,
            IReadOnlyList<string> workspaceDisambiguators,
            string? projectFolderName,
            CancellationToken cancellationToken)
        {
            var workspaces = new Dictionary<string, IWorkspace>();
            var returning = false;
            try
            {
                foreach (var kv in descriptors)
                {
                    IWorkspaceDescriptor descriptor;
                    if (!string.IsNullOrWhiteSpace(kv.Value.GitUrl.EvaluatedString) &&
                        !string.IsNullOrWhiteSpace(kv.Value.GitRef.EvaluatedString))
                    {
                        descriptor = new GitWorkspaceDescriptor
                        {
                            RepositoryUrl = kv.Value.GitUrl.EvaluatedString,
                            RepositoryCommitOrRef = kv.Value.GitRef.EvaluatedString,
                            AdditionalFolderLayers = Array.Empty<string>(),
                            AdditionalFolderZips = Array.Empty<string>(),
                            WorkspaceDisambiguators = workspaceDisambiguators,
                            ProjectFolderName = projectFolderName,
                            IsEngineBuild = false,
                            WindowsSharedGitCachePath = null,
                            MacSharedGitCachePath = null,
                        };
                    }
                    else if (!string.IsNullOrWhiteSpace(kv.Value.LocalPath))
                    {
                        descriptor = new FolderAliasWorkspaceDescriptor
                        {
                            AliasedPath = kv.Value.LocalPath,
                        };
                    }
                    else
                    {
                        throw new NotSupportedException("Workspace descriptor has no valid value!");
                    }
                    workspaces.Add(
                        kv.Key,
                        await _workspaceProvider.GetWorkspaceAsync(
                            descriptor,
                            cancellationToken).ConfigureAwait(false));
                }
                returning = true;
                return new MultiWorkspace(workspaces);
            }
            finally
            {
                if (!returning)
                {
                    foreach (var kv in workspaces)
                    {
                        await kv.Value.DisposeAsync().ConfigureAwait(false);
                    }
                }
            }
        }
    }
}
