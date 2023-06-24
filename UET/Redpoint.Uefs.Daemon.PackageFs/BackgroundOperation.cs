namespace Redpoint.Uefs.Daemon.PackageFs
{
    public class BackgroundOperation
    {
        public BackgroundOperationType Type = BackgroundOperationType.Pull;
        public Task? Task = null;
        public string PollingId = string.Empty;
        public long PullPosition = 0;
        public long VerifyPosition = 0;
        public long Length = 0;
        public BackgroundOperationStatus Status = BackgroundOperationStatus.Waiting;
        public string? Err = null;
        public string? PackagePath = null;
        public string? PackageHash = null;
        public DateTimeOffset StartTime = DateTimeOffset.UtcNow;
        public string Tag = string.Empty;
        public int? VerifyPackageIndex = null;
        public int? VerifyPackageTotal = null;
        public int VerifyChunksFixed = 0;
        public bool VerifyIsFixing = false;
        public string? GitUrl = null;
        public string? GitCommit = null;
        public string? GitServerProgressMessage = null;
        public int? GitTotalObjects = null;
        public int? GitIndexedObjects = null;
        public int? GitReceivedObjects = null;
        public long? GitReceivedBytes = null;
        public bool? GitSlowFetch = null;
    }
}
