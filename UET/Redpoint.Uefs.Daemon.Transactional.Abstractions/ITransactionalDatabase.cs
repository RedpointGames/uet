namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using System.Threading.Tasks;

    public interface ITransactionalDatabase
    {
        Task<ITransactionHandle> BeginTransactionAsync<TRequest>(
            TRequest transactionRequest,
            TransactionListenerDelegate transactionListener,
            CancellationToken cancellationToken) where TRequest : class, ITransactionRequest;

        Task<ITransactionHandle<TResult>> BeginTransactionAsync<TRequest, TResult>(
            TRequest transactionRequest,
            TransactionListenerDelegate<TResult> transactionListener,
            CancellationToken cancellationToken) where TRequest : class, ITransactionRequest<TResult> where TResult : class;

        Task<ITransactionHandle?> AddListenerToExistingTransactionAsync(
            string transactionId,
            TransactionListenerDelegate transactionListener,
            CancellationToken cancellationToken);

        Task<ITransactionHandle<TResult>?> AddListenerToExistingTransactionAsync<TResult>(
            string transactionId,
            TransactionListenerDelegate<TResult> transactionListener,
            CancellationToken cancellationToken) where TResult : class;
    }
}
