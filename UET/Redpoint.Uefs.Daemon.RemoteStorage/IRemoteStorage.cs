namespace Redpoint.Uefs.Daemon.RemoteStorage
{
    public interface IRemoteStorage<TReference>
    {
        string Type { get; }

        IRemoteStorageBlobFactory GetFactory(TReference reference);
    }
}
