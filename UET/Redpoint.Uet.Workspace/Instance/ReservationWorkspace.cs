namespace Redpoint.Uet.Workspace.Instance
{
    using Redpoint.Reservation;
    using System.Threading.Tasks;

    internal class ReservationWorkspace : IWorkspace
    {
        private readonly IReservation _reservation;

        public ReservationWorkspace(IReservation reservation)
        {
            _reservation = reservation;
        }

        public string Path => _reservation.ReservedPath;

        public ValueTask DisposeAsync()
        {
            return _reservation.DisposeAsync();
        }
    }
}
