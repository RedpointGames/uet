namespace Redpoint.Vfs.Layer.Scratch
{
    using KeyedSemaphores;
    using System.Collections.Concurrent;

    internal sealed class KeyedSemaphoresVfsLocks : IVfsLocks
    {
        private readonly KeyedSemaphoresCollection<string> _locks;
        private readonly ConcurrentDictionary<string, string> _lockHolders;

        public KeyedSemaphoresVfsLocks()
        {
            _locks = new KeyedSemaphoresCollection<string>(256);
            _lockHolders = new ConcurrentDictionary<string, string>();
        }

        public bool TryLock(string context, string normalizedKeyPath, TimeSpan timeout, Action callback, out string blockingContext)
        {
            blockingContext = "(unknown)";
            if (!_locks.TryLock(normalizedKeyPath, timeout, () =>
            {
                _lockHolders.AddOrUpdate(normalizedKeyPath, context, (_, _) => context);
                callback();
                _lockHolders.AddOrUpdate(normalizedKeyPath, "(none)", (_, _) => "(none)");
            }))
            {
                _lockHolders.TryGetValue(normalizedKeyPath, out blockingContext!);
                return false;
            }
            return true;
        }
    }
}
