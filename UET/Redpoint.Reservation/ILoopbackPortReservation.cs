namespace Redpoint.Reservation
{
    using System.Net;

    /// <summary>
    /// Represents a loopback port reservation. You must call <see cref="IAsyncDisposable.DisposeAsync"/>
    /// once you are finished with the reservation.
    /// </summary>
    public interface ILoopbackPortReservation : IAsyncDisposable
    {
        /// <summary>
        /// The loopback endpoint (address and port) which you or a child process can safely bind to,
        /// knowing that it won't be shared by any other process using the Redpoint.Reservation API.
        /// </summary>
        public IPEndPoint EndPoint { get; }
    }
}
