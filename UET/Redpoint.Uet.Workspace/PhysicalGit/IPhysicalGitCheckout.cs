namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Threading.Tasks;

    public interface IPhysicalGitCheckout
    {
        Task PrepareGitWorkspaceAsync(
            string repositoryPath,
            GitWorkspaceDescriptor descriptor,
            CancellationToken cancellationToken);
    }
}
