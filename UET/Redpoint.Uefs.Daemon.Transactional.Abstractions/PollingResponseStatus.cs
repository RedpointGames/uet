namespace Redpoint.Uefs.Daemon.Transactional.Abstractions
{
    using Redpoint.Git.Native;
    using Redpoint.Uefs.Protocol;

    public static class PollingResponseExtensions
    {
        public static void ReceiveGitUpdate(
            this PollingResponse op,
            GitFetchProgressInfo progress)
        {
            op.GitServerProgressMessage = progress.ServerProgressMessage ?? string.Empty;
            op.GitTotalObjects = progress.TotalObjects ?? 0;
            op.GitIndexedObjects = progress.IndexedObjects ?? 0;
            op.GitReceivedObjects = progress.ReceivedObjects ?? 0;
            op.GitReceivedBytes = progress.ReceivedBytes ?? 0;
            op.GitSlowFetch = progress.SlowFetch ?? false;
        }
    }
}
