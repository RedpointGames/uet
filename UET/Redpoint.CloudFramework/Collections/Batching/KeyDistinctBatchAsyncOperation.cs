namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class KeyDistinctBatchAsyncOperation<TValue, TKey, TRelated> : IBatchAsyncOperation<TValue> where TKey : notnull
    {
        private readonly Func<TValue, TKey> _keySelector;
        private readonly Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> _joiner;
        private readonly Dictionary<TKey, TRelated?> _cache;

        public KeyDistinctBatchAsyncOperation(
            Func<TValue, TKey> keySelector,
            Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner)
        {
            _keySelector = keySelector;
            _joiner = joiner;
            _cache = new Dictionary<TKey, TRelated?>();
        }

        public async Task<IReadOnlyList<object?>> ProcessBatchAsync(
            IReadOnlyList<TValue> values,
            CancellationToken cancellationToken)
        {
            var keys = values
                .Select(_keySelector)
                .Distinct()
                .Where(x => !_cache.ContainsKey(x));
            await foreach (var kv in _joiner(keys.ToAsyncEnumerable(), cancellationToken).ConfigureAwait(false))
            {
                _cache.TryAdd(kv.Key, kv.Value);
            }
            var results = new List<object?>();
            foreach (var value in values)
            {
                var key = _keySelector(value);
                if (_cache.TryGetValue(key, out var related))
                {
                    results.Add(related);
                }
                else
                {
                    results.Add(null);
                }
            }
            return results;
        }
    }
}
