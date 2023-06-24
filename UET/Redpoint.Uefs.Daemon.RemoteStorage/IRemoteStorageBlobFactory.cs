namespace Redpoint.Uefs.Daemon.RemoteStorage
{
    public interface IRemoteStorageBlobFactory : IDisposable
    {
        IRemoteStorageBlob Open();

        IRemoteStorageBlobUnsafe? OpenUnsafe();
    }
}
