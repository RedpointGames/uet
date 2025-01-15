namespace Redpoint.CloudFramework.Collections.Batching
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IBatchAsyncOperation<TValue>
    {
        Task<IReadOnlyList<object?>> ProcessBatchAsync(IReadOnlyList<TValue> values, CancellationToken cancellationToken);
    }
}
