namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using System.Threading.Tasks;

    public interface ITransactionExecutor<T> where T : ITransactionRequest
    {
        Task ExecuteTransactionAsync(ITransactionContext context, T transactionRequest, CancellationToken cancellationToken);
    }

    public interface ITransactionExecutor<T, TResult> where T : ITransactionRequest<TResult>
    {
        Task<TResult> ExecuteTransactionAsync(ITransactionContext<TResult> context, T transactionRequest, CancellationToken cancellationToken);
    }
}
