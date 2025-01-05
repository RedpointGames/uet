namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class KeyListDistinctBatchAsyncOperation<TValue, TKey, TRelated> : IBatchAsyncOperation<TValue> where TKey : notnull
    {
        private readonly Func<TValue, IReadOnlyList<TKey>> _keySelector;
        private readonly Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> _joiner;
        private readonly Dictionary<TKey, TRelated?> _cache;

        public KeyListDistinctBatchAsyncOperation(
            Func<TValue, IReadOnlyList<TKey>> keySelector,
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
                .SelectMany(_keySelector)
                .Distinct()
                .Where(x => !_cache.ContainsKey(x));
            await foreach (var kv in _joiner(keys.ToAsyncEnumerable(), cancellationToken).ConfigureAwait(false))
            {
                _cache.TryAdd(kv.Key, kv.Value);
            }
            var results = new List<object?>();
            foreach (var value in values)
            {
                var keyList = _keySelector(value);
                var valueList = new TRelated?[keyList.Count];
                for (int i = 0; i < keyList.Count; i++)
                {
                    if (!_cache.TryGetValue(keyList[i], out valueList[i]))
                    {
                        valueList[i] = default;
                    }
                }
                results.Add(valueList);
            }
            return results;
        }
    }
}
