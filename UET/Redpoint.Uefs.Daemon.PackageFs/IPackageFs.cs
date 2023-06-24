namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using System.Text.Json.Serialization.Metadata;
    using Redpoint.Uefs.Protocol;

    public interface IPackageFs : IDisposable
    {
        Task<string> PullAsync<T>(
            IRemoteStorageBlobFactory remoteStorageBlobFactory,
            string remoteStorageType,
            T remoteStorageReference,
            JsonTypeInfo<T> remoteStorageTypeInfo,
            string packageDigest,
            string extension,
            string tagHash,
            string url,
            Action releaseGlobalPullLock,
            Action<Action<PollingResponse>, string?> updatePollingResponse);

        Task VerifyAsync(
            bool isFixing,
            Action releaseGlobalPullLock,
            Action<Action<PollingResponse>> updatePollingResponse);
    }
}
