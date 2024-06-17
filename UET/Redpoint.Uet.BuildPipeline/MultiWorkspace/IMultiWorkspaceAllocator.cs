namespace Redpoint.Uet.BuildPipeline.MultiWorkspace
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IMultiWorkspaceAllocator
    {
        Task<MultiWorkspace> AllocateAsync(
            IReadOnlyDictionary<string, MultiWorkspaceDescriptor> descriptors,
            IReadOnlyList<string> workspaceDisambiguators,
            string? projectFolderName,
            CancellationToken cancellationToken);
    }
}
