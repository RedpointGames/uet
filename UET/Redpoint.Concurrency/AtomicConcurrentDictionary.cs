namespace Redpoint.Concurrency
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a dictionary which atomically adds keys from an asynchronous value factory.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    public class AtomicConcurrentDictionary<TKey, TValue>
        where TKey : notnull
    {
        class GatedData
        {
            public Gate Gate = new Gate();
            public bool Cancelled = false;
            public TValue? Value;
        }

        private ConcurrentDictionary<TKey, GatedData> _data;
        private SemaphoreSlim _dataCheck;

        /// <summary>
        /// Provides a dictionary which atomically adds keys from an asynchronous value factory.
        /// </summary>
        public AtomicConcurrentDictionary(IEqualityComparer<TKey>? comparer = null)
        {
            _data = new ConcurrentDictionary<TKey, GatedData>(comparer);
            _dataCheck = new SemaphoreSlim(1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="valueFactory"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<TValue> AtomicAddOrWaitAsync(
            TKey key,
            Func<CancellationToken, Task<TValue>> valueFactory,
            CancellationToken cancellationToken)
        {
            do
            {
                await _dataCheck.WaitAsync(cancellationToken);
                var didRelease = false;
                try
                {
                    var gatedData = new GatedData();
                    if (_data.TryAdd(key, gatedData))
                    {
                        // We added the data so we must process.
                        _dataCheck.Release();
                        didRelease = true;
                        try
                        {
                            gatedData.Value = await valueFactory(cancellationToken);
                            gatedData.Gate!.Open();
                            return gatedData.Value;
                        }
                        catch
                        {
                            _data.Remove(key, out _);
                            gatedData.Cancelled = true;
                            gatedData.Gate!.Open();
                            throw;
                        }
                    }
                    else if (_data.TryGetValue(key, out gatedData))
                    {
                        // We got an existing value, which might still be processing.
                        await gatedData.Gate.WaitAsync(cancellationToken);
                        if (gatedData.Cancelled)
                        {
                            // The thread processing the value factory failed with an
                            // exception, retry processing this.
                            continue;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        return gatedData.Value!;
                    }
                    else
                    {
                        // Race condition where we fail to add because something else is
                        // working on it, but then fail to get because the original thread
                        // failed with an exception and removed it's value. Just try processing
                        // again.
                        await Task.Yield();
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }
                }
                finally
                {
                    if (!didRelease)
                    {
                        _dataCheck.Release();
                    }
                }
            } while (true);
        }
    }
}
