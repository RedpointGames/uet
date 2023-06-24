namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    public interface ITransactionDeduplicator<TRequest> where TRequest : ITransactionRequest
    {
        bool IsDuplicateRequest(TRequest incomingRequest, ITransaction<TRequest> currentTransaction);
    }
}
