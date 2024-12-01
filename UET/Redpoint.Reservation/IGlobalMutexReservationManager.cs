namespace Redpoint.Reservation
{
    using System.Threading.Tasks;

    /// <summary>
    /// Supports making reservations on global mutexes, such that only the code using the
    /// returned <see cref="IReservation"/> has the lock of the global mutex. Mutexes are shared
    /// for the current user (not system-wide).
    /// 
    /// On Windows, this uses System.Threading.Semaphore (so it's compatible with async code). On
    /// other platforms, it uses <see cref="IReservationManager"/> and a folder path underneath
    /// the equivalent of local appdata, since the named version of System.Threading.Semaphore is
    /// not available on non-Windows platforms.
    /// </summary>
    public interface IGlobalMutexReservationManager
    {
        /// <summary>
        /// Obtain a lock on exactly the global mutex specified, waiting until the lock can
        /// be obtained.
        /// </summary>
        /// <param name="name">The name of the global mutex to obtain.</param>
        /// <param name="cancellationToken">The cancellation token which cancels the reservation operation if the global mutex has not yet been obtained.</param>
        /// <exception cref="OperationCanceledException">The cancellation token was cancelled.</exception>
        /// <returns>The reservation of the global mutex, which must be disposed to release the lock.</returns>
        ValueTask<IGlobalMutexReservation> ReserveExactAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// Try to reserve exactly the global mutex specified.
        /// </summary>
        /// <param name="name">The name of the global mutex to obtain.</param>
        /// <returns>The reservation if the lock was obtained, or null if the lock could not be obtained.</returns>
        ValueTask<IGlobalMutexReservation?> TryReserveExactAsync(string name);
    }
}
