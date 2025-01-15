namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;
    using System.Threading;

    internal class WrappedBatchedAsyncEnumerable<TValue> : IBatchedAsyncEnumerable<TValue>
    {
        private readonly IAsyncEnumerable<IReadOnlyList<TValue>> _batches;

        public WrappedBatchedAsyncEnumerable(IAsyncEnumerable<IReadOnlyList<TValue>> batches)
        {
            _batches = batches;
        }

        IAsyncEnumerator<TValue> IAsyncEnumerable<TValue>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            return _batches.SelectMany(x => x.ToAsyncEnumerable()).GetAsyncEnumerator(cancellationToken);
        }

        IAsyncEnumerable<IReadOnlyList<TValue>> IBatchedAsyncEnumerableInternal<TValue>.GetBatchingRootBatches()
        {
            return _batches;
        }

        IBatchedAsyncEnumerableInternal<TValue>? IBatchedAsyncEnumerableInternal<TValue>.GetParentBatcher()
        {
            return null;
        }

        object? IBatchedAsyncEnumerableInternal<TValue>.MapToAggregatedResult(TValue value, object? fetchedRelated, object? existingRelated)
        {
            return existingRelated;
        }

        IBatchAsyncOperation<TValue>? IBatchedAsyncEnumerableInternal<TValue>.CreateStatefulOperationForEnumerator()
        {
            return null;
        }

        public IAsyncEnumerable<IReadOnlyList<TValue>> AsBatches()
        {
            return _batches;
        }
    }
}
