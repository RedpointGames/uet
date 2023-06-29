namespace Redpoint.Reservation
{
    using System.Net;
    using System.Threading.Tasks;

    internal class DefaultLoopbackPortReservation : ILoopbackPortReservation
    {
        private readonly IPEndPoint _endpoint;
        private readonly Mutex _mutex;

        internal DefaultLoopbackPortReservation(IPEndPoint endpoint, Mutex mutex)
        {
            _endpoint = endpoint;
            _mutex = mutex;
        }

        public IPEndPoint EndPoint => _endpoint;

        public ValueTask DisposeAsync()
        {
            _mutex.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
