namespace Redpoint.CloudFramework.Collections.Batching
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal delegate object? BindingBatchedGenericMapper<TValue>(TValue value, object? fetchedRelated, object? existingRelated);

    internal class BindingBatchedAsyncEnumerable<TValue, TRelated> : IBatchedAsyncEnumerable<TValue, TRelated>
    {
        private readonly IBatchedAsyncEnumerableInternal<TValue> _parent;
        private readonly Func<IBatchAsyncOperation<TValue>> _batchOperatorFactory;
        private readonly BindingBatchedGenericMapper<TValue> _binder;

        public BindingBatchedAsyncEnumerable(
            IBatchedAsyncEnumerableInternal<TValue> parent,
            Func<IBatchAsyncOperation<TValue>> batchOperatorFactory,
            BindingBatchedGenericMapper<TValue> binder)
        {
            _parent = parent;
            _batchOperatorFactory = batchOperatorFactory;
            _binder = binder;
        }

        private class HierarchyLayer
        {
            public required int Index;
            public required IBatchedAsyncEnumerableInternal<TValue> Layer;
            public required IBatchAsyncOperation<TValue>? StatefulOperation;
            public required IReadOnlyList<object?>? LastBatchResult;
        }

        private async IAsyncEnumerable<IReadOnlyList<(TValue value, TRelated related)>> EnumerateAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            HierarchyLayer[] hierarchy;
            {
                var hierarchyCollector = new List<IBatchedAsyncEnumerableInternal<TValue>>();
                IBatchedAsyncEnumerableInternal<TValue>? current = this;
                do
                {
                    hierarchyCollector.Add(current);
                    current = current.GetParentBatcher();
                } while (current != null);
                hierarchy = new HierarchyLayer[hierarchyCollector.Count];
                for (int h = 0; h < hierarchy.Length; h++)
                {
                    // @note: hierarchyCollector will be in reverse on what we want.
                    var rh = hierarchy.Length - h - 1;
                    hierarchy[h] = new HierarchyLayer
                    {
                        Index = h,
                        Layer = hierarchyCollector[rh],
                        StatefulOperation = hierarchyCollector[rh].CreateStatefulOperationForEnumerator(),
                        LastBatchResult = null,
                    };
                }
            }

            // @note: Use hierarchy[0].Layer instead of _parent; we don't need to recurse
            // to find the root layer since we just collected all of the layers.
            await foreach (var batch in hierarchy[0].Layer.GetBatchingRootBatches().ConfigureAwait(false))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Parallel.ForEachAsync(
                    hierarchy,
                    cancellationToken,
                    async (layer, cancellationToken) =>
                    {
                        if (layer.StatefulOperation != null)
                        {
                            layer.LastBatchResult = await layer.StatefulOperation.ProcessBatchAsync(batch, cancellationToken).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
                var mappedBatch = new List<(TValue value, TRelated related)>();
                for (var i = 0; i < batch.Count; i++)
                {
                    object? value = batch[i];
                    if (value != null)
                    {
                        for (var h = 0; h < hierarchy.Length; h++)
                        {
                            if (hierarchy[h].StatefulOperation != null)
                            {
                                value = hierarchy[h].Layer.MapToAggregatedResult(
                                    batch[i],
                                    hierarchy[h].LastBatchResult![i],
                                    value);
                            }
                        }
                        if (value == null)
                        {
                            break;
                        }
                        mappedBatch.Add((batch[i], (TRelated)value!));
                    }
                }
                yield return mappedBatch;
            }
        }

        public IBatchedAsyncEnumerable<(TValue value, TRelated related)> ThenStartExecutingAsync()
        {
            return EnumerateAsync().AsBatchedAsyncEnumerable();
        }

        async IAsyncEnumerator<(TValue value, TRelated related)> IAsyncEnumerable<(TValue value, TRelated related)>.GetAsyncEnumerator(CancellationToken cancellationToken)
        {
            await foreach (var batch in EnumerateAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (var value in batch)
                {
                    yield return value;
                }
            }
        }

        IBatchedAsyncEnumerableInternal<TValue>? IBatchedAsyncEnumerableInternal<TValue>.GetParentBatcher()
        {
            return _parent;
        }

        IAsyncEnumerable<IReadOnlyList<TValue>> IBatchedAsyncEnumerableInternal<TValue>.GetBatchingRootBatches()
        {
            return _parent.GetBatchingRootBatches();
        }

        object? IBatchedAsyncEnumerableInternal<TValue>.MapToAggregatedResult(TValue value, object? fetchedRelated, object? existingRelated)
        {
            return _binder(value, fetchedRelated, existingRelated);
        }

        IBatchAsyncOperation<TValue> IBatchedAsyncEnumerableInternal<TValue>.CreateStatefulOperationForEnumerator()
        {
            return _batchOperatorFactory();
        }
    }
}
