namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;

    public record class ListMountsTransactionRequest : ITransactionRequest<ListResponse>
    {
    }
}
