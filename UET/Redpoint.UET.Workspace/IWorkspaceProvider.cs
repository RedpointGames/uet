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
            WorkspaceOptions workspaceOptions,
            CancellationToken cancellationToken);

        Task<IWorkspace> GetPackageWorkspaceAsync(
            string tag,
            string workspaceSuffix,
            WorkspaceOptions workspaceOptions,
            CancellationToken cancellationToken);

        Task<IWorkspace> GetFolderWorkspaceAsync(
            string path, 
            string[] disambiguators,
            WorkspaceOptions workspaceOptions);

        Task<IWorkspace> GetTempWorkspaceAsync(string name);
    }
}
