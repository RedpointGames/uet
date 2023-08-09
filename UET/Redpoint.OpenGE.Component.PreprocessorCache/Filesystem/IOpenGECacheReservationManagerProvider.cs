namespace Redpoint.OpenGE.Component.PreprocessorCache.Filesystem
{
    using Redpoint.Reservation;

    internal interface IOpenGECacheReservationManagerProvider
    {
        IReservationManager ReservationManager { get; }
    }
}
