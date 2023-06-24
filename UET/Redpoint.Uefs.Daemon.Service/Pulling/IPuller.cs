namespace Redpoint.Uefs.Daemon.Service.Pulling
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading.Tasks;

    internal record PullResult(string TransactionId, bool Complete);

    internal interface IPuller<TRequest>
    {
        Task<PullResult> PullAsync(
            IUefsDaemon daemon,
            TRequest request,
            TransactionListenerDelegate<FileInfo?> onPollingResponse, 
            CancellationToken cancellationToken);
    }
}
