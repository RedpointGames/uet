namespace Redpoint.Uet.Workspace.Reservation
{
    using Redpoint.Reservation;

    public interface IReservationManagerForUet : IReservationManager
    {
        /// <summary>
        /// The directory under which reservations are made.
        /// </summary>
        string RootPath { get; }
    }
}
