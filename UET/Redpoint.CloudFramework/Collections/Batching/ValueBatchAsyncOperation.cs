namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal class ValueBatchAsyncOperation<TValue, TRelated> : IBatchAsyncOperation<TValue>
    {
        private readonly Func<IReadOnlyList<TValue>, CancellationToken, Task<IReadOnlyList<TRelated?>>> _joiner;

        public ValueBatchAsyncOperation(
            Func<IReadOnlyList<TValue>, CancellationToken, Task<IReadOnlyList<TRelated?>>> joiner)
        {
            _joiner = joiner;
        }

        public async Task<IReadOnlyList<object?>> ProcessBatchAsync(
            IReadOnlyList<TValue> values,
            CancellationToken cancellationToken)
        {
            var results = new List<object?>();
            foreach (var entry in await _joiner(values, cancellationToken).ConfigureAwait(false))
            {
                results.Add(entry);
            }
            if (results.Count != values.Count)
            {
                throw new InvalidOperationException("JoinByValueAwait joiner must return exactly the same number of elements as the batch input.");
            }
            return results;
        }
    }
}
