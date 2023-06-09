namespace Redpoint.UET.Workspace
{
    using Redpoint.UET.Workspace.Descriptors;
    using System.Threading.Tasks;

    public interface IWorkspaceProviderBase
    {
        bool ProvidesFastCopyOnWrite { get; }

        Task<IWorkspace> GetWorkspaceAsync(
            IWorkspaceDescriptor workspaceDescriptor,
            CancellationToken cancellationToken);
    }
}
