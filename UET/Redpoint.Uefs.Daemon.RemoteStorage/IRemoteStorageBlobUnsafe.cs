namespace Redpoint.Uefs.Daemon.RemoteStorage
{
    using Microsoft.Win32.SafeHandles;

    public interface IRemoteStorageBlobUnsafe : IDisposable
    {
        SafeFileHandle SafeFileHandle { get; }
    }
}
