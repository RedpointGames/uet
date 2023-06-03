namespace Redpoint.UET.Workspace
{
    using Redpoint.Reservation;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultReservationManagerForUET : IReservationManagerForUET
    {
        private readonly IReservationManager _reservationManager;

        public DefaultReservationManagerForUET(IReservationManager reservationManager)
        {
            _reservationManager = reservationManager;
        }

        public Task<IReservation> ReserveAsync(string @namespace, params string[] parameters)
        {
            return _reservationManager.ReserveAsync(@namespace, parameters);
        }

        public Task<IReservation> ReserveExactAsync(string name, CancellationToken cancellationToken)
        {
            return _reservationManager.ReserveExactAsync(name, cancellationToken);
        }

        public Task<IReservation?> TryReserveExactAsync(string name)
        {
            return _reservationManager.TryReserveExactAsync(name);
        }
    }
}
