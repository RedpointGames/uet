namespace Redpoint.UET.Workspace.PhysicalGit
{
    using Redpoint.Reservation;
    using Redpoint.UET.Workspace.Descriptors;
    using System.Threading.Tasks;

    internal interface IPhysicalGitCheckout
    {
        Task PrepareGitWorkspaceAsync(IReservation reservation, GitWorkspaceDescriptor descriptor, CancellationToken cancellationToken);
    }
}
