namespace Redpoint.Reservation
{
    using System.Threading.Tasks;

    /// <summary>
    /// Supports making reservations such that only the code using the returned 
    /// <see cref="IReservation"/> will be accessing the folder pointed to by 
    /// the reservation. The locking mechanism for the reservation manager is 
    /// both thread-aware and process-aware.
    /// </summary>
    public interface IReservationManager
    {
        /// <summary>
        /// Reserve a directory. The <paramref name="namespace"/> and <paramref name="parameters"/>
        /// are used to generate a unique prefix, and the first available folder with this prefix 
        /// is reserved. If there are no available folders with this prefix, a new folder is 
        /// allocated and returned.
        /// 
        /// The layout of the parameters should be the same for the same namespace.
        /// </summary>
        /// <param name="namespace">The namespace or type of reservation used to make a unique prefix.</param>
        /// <param name="parameters">The parameters to the reservation that are used to make a unique prefix.</param>
        /// <returns></returns>
        Task<IReservation> ReserveAsync(string @namespace, params string[] parameters);

        /// <summary>
        /// Reserve exactly the directory with the <paramref name="name"/>. If the directory is already
        /// reserved, this continously polls until the reservation can be made or until the
        /// <paramref name="cancellationToken"/> is cancelled.
        /// </summary>
        /// <param name="name">The ID to reserve.</param>
        /// <param name="cancellationToken"></param>
        /// <returns>The reservation.</returns>
        /// <exception cref="OperationCanceledException">The task was cancelled before the reservation could be made.</exception>
        Task<IReservation> ReserveExactAsync(string name, CancellationToken cancellationToken);

        /// <summary>
        /// Try to reserve exactly the directory with the <paramref name="name"/>. If the directory is already
        /// reserved, this returns null.
        /// </summary>
        /// <param name="name">The ID to reserve.</param>
        /// <returns>The reservation if it was made.</returns>
        Task<IReservation?> TryReserveExactAsync(string name);
    }
}
