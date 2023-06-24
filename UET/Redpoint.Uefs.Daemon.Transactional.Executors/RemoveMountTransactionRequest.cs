namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;

    public record class RemoveMountTransactionRequest : ITransactionRequest
    {
        public required string MountId { get; set; }
    }
}
