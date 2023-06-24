namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.PackageFs;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;

    public record class PullPackageTagTransactionRequest : ITransactionRequest<PullPackageTagTransactionResult>, IBackgroundableTransactionRequest
    {
        public required IPackageFs PackageFs { get; set; }
        public required string Tag { get; set; }
        public required RegistryCredential Credential { get; set; }
        public required bool NoWait { get; set; }
    }
}
