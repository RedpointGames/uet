namespace Redpoint.Vfs.Layer.Scratch
{
    using System.Collections.Concurrent;

    internal class SemaphoreSlimVfsLocks : IVfsLocks
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
        private readonly ConcurrentDictionary<string, string> _lockHolders;

        public SemaphoreSlimVfsLocks()
        {
            _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _lockHolders = new ConcurrentDictionary<string, string>();
        }

        public bool TryLock(string context, string normalizedKeyPath, TimeSpan timeout, Action callback, out string blockingContext)
        {
            blockingContext = "(unknown)";
            var semaphore = _locks.GetOrAdd(normalizedKeyPath, new SemaphoreSlim(1));
            if (semaphore.Wait(timeout == TimeSpan.MaxValue ? -1 : (int)timeout.TotalMilliseconds))
            {
                try
                {
                    _lockHolders.AddOrUpdate(normalizedKeyPath, context, (_, _) => context);
                    callback();
                    _lockHolders.AddOrUpdate(normalizedKeyPath, "(none)", (_, _) => "(none)");
                    return true;
                }
                finally
                {
                    semaphore.Release();
                }
            }
            else
            {
                _lockHolders.TryGetValue(normalizedKeyPath, out blockingContext!);
                return false;
            }
        }
    }
}
