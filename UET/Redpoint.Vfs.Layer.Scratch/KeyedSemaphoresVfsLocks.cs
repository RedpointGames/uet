namespace Redpoint.Vfs.Layer.Scratch
{
    using AsyncKeyedLock;
    using System.Collections.Concurrent;

    internal sealed class KeyedSemaphoresVfsLocks : IVfsLocks, IDisposable
    {
        private readonly StripedAsyncKeyedLocker<string> _locks;
        private readonly ConcurrentDictionary<string, string> _lockHolders;

        public KeyedSemaphoresVfsLocks()
        {
            _locks = new StripedAsyncKeyedLocker<string>(256);
            _lockHolders = new ConcurrentDictionary<string, string>();
        }

        public void Dispose()
        {
            _locks.Dispose();
        }

        public bool TryLock(string context, string normalizedKeyPath, TimeSpan timeout, Action callback, out string blockingContext)
        {
            blockingContext = "(unknown)";
            if (!_locks.TryLock(normalizedKeyPath, () =>
            {
                _lockHolders.AddOrUpdate(normalizedKeyPath, context, (_, _) => context);
                callback();
                _lockHolders.AddOrUpdate(normalizedKeyPath, "(none)", (_, _) => "(none)");
            }, timeout))
            {
                _lockHolders.TryGetValue(normalizedKeyPath, out blockingContext!);
                return false;
            }
            return true;
        }
    }
}
