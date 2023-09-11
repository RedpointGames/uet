namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using Redpoint.Uefs.Protocol;

    public delegate Task TransactionListener(PollingResponse nextResponse);

    public delegate Task TransactionListener<T>(PollingResponse nextResponse, T? result);
}
