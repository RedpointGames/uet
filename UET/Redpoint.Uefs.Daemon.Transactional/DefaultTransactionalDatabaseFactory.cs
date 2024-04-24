namespace Redpoint.Uefs.Daemon.Transactional
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;

    internal sealed class DefaultTransactionalDatabaseFactory : ITransactionalDatabaseFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DefaultTransactionalDatabase> _logger;

        public DefaultTransactionalDatabaseFactory(
            IServiceProvider serviceProvider,
            ILogger<DefaultTransactionalDatabase> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public ITransactionalDatabase CreateTransactionalDatabase()
        {
            return new DefaultTransactionalDatabase(_serviceProvider, _logger);
        }
    }
}
