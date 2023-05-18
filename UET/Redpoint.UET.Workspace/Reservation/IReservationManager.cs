namespace Redpoint.UET.Workspace.Reservation
{
    using System.Threading.Tasks;

    public interface IReservationManager
    {
        Task<IReservation> ReserveAsync(string classification, params string[] parameters);
    }
}
