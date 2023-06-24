namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using System.Threading.Tasks;

    public interface IMountLockObtainer
    {
        Task<IDisposable> ObtainLockAsync(
            ITransactionContext context,
            string operationName,
            CancellationToken cancellationToken);
    }
}
