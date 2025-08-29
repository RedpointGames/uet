namespace Redpoint.Uet.Workspace.Reservation
{
    using Redpoint.Reservation;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultReservationManagerForUet : IReservationManagerForUet
    {
        private readonly IReservationManager _reservationManager;

        public DefaultReservationManagerForUet(
            IReservationManager reservationManager,
            string path)
        {
            _reservationManager = reservationManager;
            RootPath = path;
        }

        public string RootPath { get; }

        public Task<IReservation> ReserveAsync(string @namespace, params string[] parameters)
        {
            return _reservationManager.ReserveAsync(@namespace, parameters);
        }

        public Task<IReservation> ReserveExactAsync(string name, CancellationToken cancellationToken, bool? hold)
        {
            return _reservationManager.ReserveExactAsync(name, cancellationToken, hold);
        }

        public IReservation? TryReserveExact(string name, bool? hold)
        {
            return _reservationManager.TryReserveExact(name, hold);
        }

        public Task<IReservation?> TryReserveExactAsync(string name, bool? hold)
        {
            return _reservationManager.TryReserveExactAsync(name, hold);
        }
    }
}
