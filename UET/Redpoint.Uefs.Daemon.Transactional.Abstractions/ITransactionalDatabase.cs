namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using System.Threading.Tasks;

    public interface ITransactionalDatabase
    {
        Task<ITransactionHandle> BeginTransactionAsync<TRequest>(
            TRequest transactionRequest,
            TransactionListener transactionListener,
            CancellationToken cancellationToken) where TRequest : class, ITransactionRequest;

        Task<ITransactionHandle<TResult>> BeginTransactionAsync<TRequest, TResult>(
            TRequest transactionRequest,
            TransactionListener<TResult> transactionListener,
            CancellationToken cancellationToken) where TRequest : class, ITransactionRequest<TResult> where TResult : class;

        Task<ITransactionHandle?> AddListenerToExistingTransactionAsync(
            string transactionId,
            TransactionListener transactionListener,
            CancellationToken cancellationToken);

        Task<ITransactionHandle<TResult>?> AddListenerToExistingTransactionAsync<TResult>(
            string transactionId,
            TransactionListener<TResult> transactionListener,
            CancellationToken cancellationToken) where TResult : class;
    }
}
