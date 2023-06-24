namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    public record class PullPackageTagTransactionResult
    {
        public required FileInfo PackagePath { get; set; }
    }
}
