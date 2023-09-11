namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Necessary empty interface for typing.")]
    public interface ITransactionRequest
    {
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1040:Avoid empty interfaces", Justification = "Necessary empty interface for typing.")]
    public interface ITransactionRequest<TResult> : ITransactionRequest
    {
    }

    public interface IBackgroundableTransactionRequest
    {
        bool NoWait { get; }
    }
}
