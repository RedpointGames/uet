namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Redpoint.Uefs.Daemon.PackageFs;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;

    public record class VerifyPackagesTransactionRequest : ITransactionRequest, IBackgroundableTransactionRequest
    {
        public required IPackageFs PackageFs { get; set; }

        public required bool Fix { get; set; }

        public required bool NoWait { get; set; }
    }
}
