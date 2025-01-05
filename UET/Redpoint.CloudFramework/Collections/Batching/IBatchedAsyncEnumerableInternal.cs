namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;

    public interface IBatchedAsyncEnumerableInternal<TValue>
    {
        internal IAsyncEnumerable<IReadOnlyList<TValue>> GetBatchingRootBatches();

        internal IBatchedAsyncEnumerableInternal<TValue>? GetParentBatcher();

        internal object? MapToAggregatedResult(TValue value, object? fetchedRelated, object? existingRelated);

        internal IBatchAsyncOperation<TValue>? CreateStatefulOperationForEnumerator();
    }
}
