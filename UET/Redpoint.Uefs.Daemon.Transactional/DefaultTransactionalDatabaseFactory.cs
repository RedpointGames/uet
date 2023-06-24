namespace Redpoint.Uefs.Daemon.Transactional
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;

    internal class DefaultTransactionalDatabaseFactory : ITransactionalDatabaseFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public DefaultTransactionalDatabaseFactory(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ITransactionalDatabase CreateTransactionalDatabase()
        {
            return new DefaultTransactionalDatabase(_serviceProvider);
        }
    }
}
