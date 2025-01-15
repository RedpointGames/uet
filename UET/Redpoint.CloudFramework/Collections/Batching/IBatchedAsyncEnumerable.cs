namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;

    public interface IBatchedAsyncEnumerable<TValue> : IAsyncEnumerable<TValue>, IBatchedAsyncEnumerableInternal<TValue>
    {
        IAsyncEnumerable<IReadOnlyList<TValue>> AsBatches();
    }

    public interface IBatchedAsyncEnumerable<TValue, TRelated> : IAsyncEnumerable<(TValue value, TRelated related)>, IBatchedAsyncEnumerableInternal<TValue>
    {
        IBatchedAsyncEnumerable<(TValue value, TRelated related)> ThenStartExecutingAsync();
    }
}
