namespace Redpoint.Reservation
{
    using System.Threading.Tasks;

    /// <summary>
    /// Supports making loopback port reservations such the loopback endpoint returned
    /// will be unique to all callers using the <see cref="ILoopbackPortReservationManager"/>
    /// API. Other processes can still bind to the returned address, as this API is meant
    /// for scenarios where you need to reserve a port in advance for a child process to
    /// use.
    /// </summary>
    public interface ILoopbackPortReservationManager
    {
        /// <summary>
        /// Reserve a port on a loopback interface. The interface is chosen based on the process ID
        /// and a randomly generated unique ID.
        /// </summary>
        /// <remarks>
        /// Internally this uses a system-wide mutex to ensure that no other process can reserve the 
        /// same loopback endpoint as another process using the Redpoint.Reservation library.
        /// 
        /// The loopback address returned will <em>not</em> be 127.0.0.1, but rather another address
        /// in the 127.0.0.0/8 loopback address space.
        /// </remarks>
        ValueTask<ILoopbackPortReservation> ReserveAsync();
    }
}
