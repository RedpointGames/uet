namespace Redpoint.Reservation
{
    /// <summary>
    /// Allows you to create new <see cref="IReservationManager"/> instances.
    /// </summary>
    public interface IReservationManagerFactory
    {
        /// <summary>
        /// Creates a new <see cref="IReservationManager"/> with the specified <see cref="rootPath"/>.
        /// </summary>
        /// <param name="rootPath">The path under which to make reservations.</param>
        /// <returns>The new reservation manager.</returns>
        IReservationManager CreateReservationManager(string rootPath);
    }
}
