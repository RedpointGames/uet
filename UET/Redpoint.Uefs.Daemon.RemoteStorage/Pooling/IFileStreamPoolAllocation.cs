namespace Redpoint.Uefs.Daemon.RemoteStorage.Pooling
{
    public interface IFileStreamPoolAllocation : IDisposable
    {
        FileStream FileStream { get; }
    }
}
