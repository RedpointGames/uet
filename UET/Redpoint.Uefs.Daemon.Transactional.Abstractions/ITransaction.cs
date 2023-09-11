namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using Redpoint.Uefs.Protocol;
    using System;

    public interface ITransaction
    {
        object RequestUnknown { get; }

        PollingResponse LatestPollingResponse { get; }

        void UpdatePollingResponse(PollingResponse pollingResponse);
    }

    public interface ITransaction<TRequest> : ITransaction where TRequest : ITransactionRequest
    {
        TRequest Request { get; }

        IAsyncDisposable RegisterListener(TransactionListener listenerDelegate);
    }

    public interface ITransaction<TRequest, TResult> : ITransaction<TRequest> where TRequest : ITransactionRequest<TResult>
    {
        TResult? Result { get; }

        void UpdatePollingResponse(PollingResponse pollingResponse, TResult? result);

        IAsyncDisposable RegisterListener(TransactionListener<TResult> listenerDelegate);
    }
}
