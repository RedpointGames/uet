namespace Redpoint.Vfs.Layer.Scratch
{
    internal interface IVfsLocks
    {
        bool TryLock(string context, string normalizedKeyPath, TimeSpan timeout, Action callback, out string blockingContext);
    }
}
