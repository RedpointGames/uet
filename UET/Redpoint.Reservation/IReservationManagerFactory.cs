namespace Redpoint.Reservation
{
    /// <summary>
    /// Allows you to create new <see cref="IReservationManager"/> instances.
    /// </summary>
    public interface IReservationManagerFactory
    {
        /// <summary>
        /// Creates a new <see cref="IReservationManager"/> with the specified <paramref name="rootPath"/>.
        /// </summary>
        /// <param name="rootPath">The path under which to make reservations.</param>
        /// <returns>The new reservation manager.</returns>
        IReservationManager CreateReservationManager(string rootPath);

        /// <summary>
        /// Creates a new <see cref="ILoopbackPortReservationManager"/>.
        /// </summary>
        /// <returns>The new loopback port reservation manager.</returns>
        ILoopbackPortReservationManager CreateLoopbackPortReservationManager();

        /// <summary>
        /// Creates a new <see cref="IGlobalMutexReservationManager"/>.
        /// </summary>
        /// <returns>The new global mutex reservation manager.</returns>
        IGlobalMutexReservationManager CreateGlobalMutexReservationManager();
    }
}
