namespace Redpoint.Reservation
{
    /// <summary>
    /// Represents a lock obtained on a global mutex. You must call <see cref="IAsyncDisposable.DisposeAsync"/>
    /// once you are finished with the reservation.
    /// </summary>
    public interface IGlobalMutexReservation : IAsyncDisposable
    {
    }
}
