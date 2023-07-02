namespace Redpoint.Reservation
{
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultLoopbackPortReservation : ILoopbackPortReservation
    {
        private readonly IPEndPoint _endpoint;
        private readonly IGlobalMutexReservation _reservation;

        internal DefaultLoopbackPortReservation(IPEndPoint endpoint, IGlobalMutexReservation reservation)
        {
            _endpoint = endpoint;
            _reservation = reservation;
        }

        public IPEndPoint EndPoint => _endpoint;

        public ValueTask DisposeAsync()
        {
            return _reservation.DisposeAsync();
        }
    }
}
