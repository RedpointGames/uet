namespace Redpoint.Uet.Workspace.Storage
{
    using System;
    using System.Threading.Tasks;

    public interface IStorageManagement
    {
        Task<ListStorageResult> ListStorageAsync(
            bool includeDiskUsage,
            Action<int> onStart,
            Action<(int total, int remaining)> onProgress,
            CancellationToken cancellationToken);

        Task PurgeStorageAsync(
            bool performDeletion,
            int daysThreshold,
            CancellationToken cancellationToken);

        Task AutoPurgeStorageAsync(
            bool verbose,
            CancellationToken cancellationToken);
    }
}
