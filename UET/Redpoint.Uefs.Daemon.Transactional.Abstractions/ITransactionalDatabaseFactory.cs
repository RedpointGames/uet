namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    public interface ITransactionalDatabaseFactory
    {
        ITransactionalDatabase CreateTransactionalDatabase();
    }
}
