namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    public interface ITransactionRequest
    {
    }

    public interface ITransactionRequest<TResult> : ITransactionRequest
    {
    }

    public interface IBackgroundableTransactionRequest
    {
        bool NoWait { get; }
    }
}
