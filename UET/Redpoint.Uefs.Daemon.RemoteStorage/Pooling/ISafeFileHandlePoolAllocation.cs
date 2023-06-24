namespace Redpoint.Uefs.Daemon.RemoteStorage.Pooling
{
    using Microsoft.Win32.SafeHandles;

    public interface ISafeFileHandlePoolAllocation : IDisposable
    {
        SafeFileHandle SafeFileHandle { get; }
    }
}
