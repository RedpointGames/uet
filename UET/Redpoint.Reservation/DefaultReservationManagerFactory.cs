namespace Redpoint.Reservation
{
    using Microsoft.Extensions.Logging;

    internal class DefaultReservationManagerFactory : IReservationManagerFactory
    {
        private readonly ILogger<DefaultReservationManager> _logger;

        public DefaultReservationManagerFactory(
            ILogger<DefaultReservationManager> logger)
        {
            _logger = logger;
        }

        public IReservationManager CreateReservationManager(string rootPath)
        {
            return new DefaultReservationManager(_logger, rootPath);
        }

        public ILoopbackPortReservationManager CreateLoopbackPortReservationManager()
        {
            return new DefaultLoopbackPortReservationManager(CreateGlobalMutexReservationManager());
        }

        public IGlobalMutexReservationManager CreateGlobalMutexReservationManager()
        {
            return new DefaultGlobalMutexReservationManager(this);
        }
    }
}
