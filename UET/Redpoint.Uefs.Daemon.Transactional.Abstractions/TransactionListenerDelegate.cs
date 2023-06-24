namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using Redpoint.Uefs.Protocol;

    public delegate Task TransactionListenerDelegate(PollingResponse nextResponse);

    public delegate Task TransactionListenerDelegate<T>(PollingResponse nextResponse, T? result);
}
