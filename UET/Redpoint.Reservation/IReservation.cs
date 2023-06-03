namespace Redpoint.Reservation
{
    using System;

    /// <summary>
    /// Represents a folder reservation. You must call <see cref="IAsyncDisposable.DisposeAsync"/>
    /// once you are finished with the reservation.
    /// </summary>
    public interface IReservation : IAsyncDisposable
    {
        /// <summary>
        /// The directory under which you can safely read and write data. The reservation manager
        /// ensures that no other code using <see cref="IReservationManager"/> will have the same
        /// folder reserved at the same time, even across processes.
        /// </summary>
        string ReservedPath { get; }
    }
}
