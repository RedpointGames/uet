namespace Redpoint.UET.Workspace
{
    using System.Threading.Tasks;

    public interface IWorkspaceProvider
    {
        Task<IWorkspace> GetGitWorkspaceAsync(
            string repository,
            string commit,
            string[] folders,
            string workspaceSuffix,
            CancellationToken cancellationToken);

        Task<IWorkspace> GetPackageWorkspaceAsync(
            string tag,
            string workspaceSuffix,
            CancellationToken cancellationToken);

        Task<IWorkspace> GetLocalWorkspaceAsync();

        Task<IWorkspace> GetTempWorkspaceAsync(string name);
    }
}
