namespace Redpoint.Uet.Workspace
{
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Threading.Tasks;

    public interface IWorkspaceProviderBase
    {
        Task<IWorkspace> GetWorkspaceAsync(
            IWorkspaceDescriptor workspaceDescriptor,
            CancellationToken cancellationToken);
    }
}
