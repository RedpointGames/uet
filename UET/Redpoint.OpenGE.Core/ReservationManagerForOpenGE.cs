namespace Redpoint.OpenGE.Core
{
    using Redpoint.Reservation;
    using Redpoint.Uet.CommonPaths;

    internal sealed class ReservationManagerForOpenGE : IReservationManagerForOpenGE
    {
        private readonly string _rootDirectory;
        private readonly IReservationManager _reservationManager;

        public string RootDirectory => _rootDirectory;

        public ReservationManagerForOpenGE(
            IReservationManagerFactory reservationManagerFactory)
        {
            _rootDirectory = UetPaths.OpenGEUserSpecificCachePath;
            _reservationManager = reservationManagerFactory.CreateReservationManager(_rootDirectory);
        }

        public IReservationManager ReservationManager => _reservationManager;
    }
}
