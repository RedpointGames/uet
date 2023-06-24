namespace Redpoint.Uefs.Daemon.Service.Mounting
{
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using System.Threading.Tasks;

    internal interface IMounter<TRequest>
    {
        Task MountAsync(
            IUefsDaemon daemon, 
            MountContext context, 
            TRequest request,
            TransactionListenerDelegate onPollingResponse,
            CancellationToken cancellationToken);
    }
}
