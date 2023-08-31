namespace Redpoint.OpenGE.Core
{
    using Redpoint.Reservation;

    public interface IReservationManagerForOpenGE
    {
        string RootDirectory { get; }

        IReservationManager ReservationManager { get; }
    }
}
