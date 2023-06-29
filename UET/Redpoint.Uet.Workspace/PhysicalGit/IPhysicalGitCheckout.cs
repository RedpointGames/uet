namespace Redpoint.Uet.Workspace.PhysicalGit
{
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace.Descriptors;
    using System.Threading.Tasks;

    internal interface IPhysicalGitCheckout
    {
        Task PrepareGitWorkspaceAsync(IReservation reservation, GitWorkspaceDescriptor descriptor, CancellationToken cancellationToken);
    }
}
