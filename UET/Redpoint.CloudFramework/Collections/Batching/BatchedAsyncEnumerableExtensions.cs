namespace Redpoint.CloudFramework.Collections.Batching
{
    using System;
    using System.Collections.Generic;
    using System.Threading;

    public static class BatchedAsyncEnumerableExtensions
    {
        /// <summary>
        /// Wraps the value as an asynchronous enumerable of a batch with a single entry.
        /// </summary>
        public static IBatchedAsyncEnumerable<TValue> AsSingleBatchedAsyncEnumerable<TValue>(TValue value)
        {
            return new WrappedBatchedAsyncEnumerable<TValue>(new[]
            {
                new TValue[1] { value }
            }.ToAsyncEnumerable());
        }

        /// <summary>
        /// Wraps the asynchronous enumerable of lists as a batched asynchronous enumerable.
        /// </summary>
        /// <typeparam name="TValue">The value type inside each batch.</typeparam>
        /// <param name="batches">The asynchronous enumerable that emits batches of values.</param>
        /// <returns>A new batched asynchronous enumerable.</returns>
        public static IBatchedAsyncEnumerable<TValue> AsBatchedAsyncEnumerable<TValue>(this IAsyncEnumerable<IReadOnlyList<TValue>> batches)
        {
            return new WrappedBatchedAsyncEnumerable<TValue>(batches);
        }

        // - cancellation token
        // - single key
        // - first join
        public static IBatchedAsyncEnumerable<TValue, TRelated?> JoinByDistinctKeyAwait<TValue, TKey, TRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<TValue, TKey> keySelector,
            Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner) where TKey : notnull
        {
            return new BindingBatchedAsyncEnumerable<TValue, TRelated?>(
                enumerable,
                () => new KeyDistinctBatchAsyncOperation<TValue, TKey, TRelated>(
                    keySelector,
                    joiner),
                (value, fetchedValue, _) => fetchedValue);
        }

        // - cancellation token
        // - multiple key
        // - first join
        public static IBatchedAsyncEnumerable<TValue, IReadOnlyList<TRelated?>> JoinByDistinctKeyListAwait<TValue, TKey, TRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<TValue, IReadOnlyList<TKey>> keySelector,
            Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner) where TKey : notnull
        {
            return new BindingBatchedAsyncEnumerable<TValue, IReadOnlyList<TRelated?>>(
                enumerable,
                () => new KeyListDistinctBatchAsyncOperation<TValue, TKey, TRelated>(
                    keySelector,
                    joiner),
                (value, fetchedValue, _) => fetchedValue);
        }

        // - no cancellation token
        // - single key
        // - first join
        public static IBatchedAsyncEnumerable<TValue, TRelated?> JoinByDistinctKeyAwait<TValue, TKey, TRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<TValue, TKey> keySelector,
            Func<IAsyncEnumerable<TKey>, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner) where TKey : notnull
            => JoinByDistinctKeyAwait(
                enumerable,
                keySelector,
                (values, _) => joiner(values));

        // - no cancellation token
        // - multiple key
        // - first join
        public static IBatchedAsyncEnumerable<TValue, IReadOnlyList<TRelated?>> JoinByDistinctKeyListAwait<TValue, TKey, TRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<TValue, IReadOnlyList<TKey>> keySelector,
            Func<IAsyncEnumerable<TKey>, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner) where TKey : notnull
            => JoinByDistinctKeyListAwait(
                enumerable,
                keySelector,
                (values, _) => joiner(values));

        // - cancellation token
        // - single key
        // - subsequent join
        public static IBatchedAsyncEnumerable<TValue, TAggregateRelated> JoinByDistinctKeyAwait<TValue, TKey, TExistingRelated, TRelated, TAggregateRelated>(
            this IBatchedAsyncEnumerable<TValue, TExistingRelated> enumerable,
            Func<TValue, TKey> keySelector,
            Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner,
            Func<TExistingRelated, TRelated?, TAggregateRelated> binder) where TKey : notnull where TAggregateRelated : notnull
        {
            return new BindingBatchedAsyncEnumerable<TValue, TAggregateRelated>(
                enumerable,
                () => new KeyDistinctBatchAsyncOperation<TValue, TKey, TRelated>(
                    keySelector,
                    joiner),
                (value, fetchedValue, parentValue) => binder((TExistingRelated)parentValue!, (TRelated?)fetchedValue));
        }

        // - cancellation token
        // - multiple key
        // - subsequent join
        public static IBatchedAsyncEnumerable<TValue, TAggregateRelated> JoinByDistinctKeyListAwait<TValue, TKey, TExistingRelated, TRelated, TAggregateRelated>(
            this IBatchedAsyncEnumerable<TValue, TExistingRelated> enumerable,
            Func<TValue, IReadOnlyList<TKey>> keySelector,
            Func<IAsyncEnumerable<TKey>, CancellationToken, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner,
            Func<TExistingRelated, IReadOnlyList<TRelated?>, TAggregateRelated> binder) where TKey : notnull where TAggregateRelated : notnull
        {
            return new BindingBatchedAsyncEnumerable<TValue, TAggregateRelated>(
                enumerable,
                () => new KeyListDistinctBatchAsyncOperation<TValue, TKey, TRelated>(
                    keySelector,
                    joiner),
                (value, fetchedValue, parentValue) => binder((TExistingRelated)parentValue!, (IReadOnlyList<TRelated?>)fetchedValue!));
        }

        // - no cancellation token
        // - single key
        // - subsequent join
        public static IBatchedAsyncEnumerable<TValue, TAggregateRelated> JoinByDistinctKeyAwait<TValue, TKey, TExistingRelated, TRelated, TAggregateRelated>(
            this IBatchedAsyncEnumerable<TValue, TExistingRelated> enumerable,
            Func<TValue, TKey> keySelector,
            Func<IAsyncEnumerable<TKey>, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner,
            Func<TExistingRelated, TRelated?, TAggregateRelated> binder) where TKey : notnull where TAggregateRelated : notnull
            => JoinByDistinctKeyAwait(
                enumerable,
                keySelector,
                (values, _) => joiner(values),
                binder);

        // - no cancellation token
        // - multiple key
        // - subsequent join
        public static IBatchedAsyncEnumerable<TValue, TAggregateRelated> JoinByDistinctKeyListAwait<TValue, TKey, TExistingRelated, TRelated, TAggregateRelated>(
            this IBatchedAsyncEnumerable<TValue, TExistingRelated> enumerable,
            Func<TValue, IReadOnlyList<TKey>> keySelector,
            Func<IAsyncEnumerable<TKey>, IAsyncEnumerable<KeyValuePair<TKey, TRelated?>>> joiner,
            Func<TExistingRelated, IReadOnlyList<TRelated?>, TAggregateRelated> binder) where TKey : notnull where TAggregateRelated : notnull
            => JoinByDistinctKeyListAwait(
                enumerable,
                keySelector,
                (values, _) => joiner(values),
                binder);

        public static IBatchedAsyncEnumerable<TValue, TRelated> JoinByValueAwait<TValue, TRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<IReadOnlyList<TValue>, CancellationToken, Task<IReadOnlyList<TRelated?>>> joiner)
        {
            return new BindingBatchedAsyncEnumerable<TValue, TRelated>(
                enumerable,
                () => new ValueBatchAsyncOperation<TValue, TRelated>(joiner),
                (value, fetchedValue, _) => fetchedValue);
        }

        public static IBatchedAsyncEnumerable<TValue, TRelated> JoinByValueAwait<TValue, TRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<IReadOnlyList<TValue>, Task<IReadOnlyList<TRelated?>>> joiner)
        {
            return new BindingBatchedAsyncEnumerable<TValue, TRelated>(
                enumerable,
                () => new ValueBatchAsyncOperation<TValue, TRelated>((values, _) => joiner(values)),
                (value, fetchedValue, _) => fetchedValue);
        }

        public static IBatchedAsyncEnumerable<TValue, TRelated> JoinByValueAwait<TValue, TExistingRelated, TRelated, TAggregateRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<IReadOnlyList<TValue>, CancellationToken, Task<IReadOnlyList<TRelated?>>> joiner,
            Func<TExistingRelated, TRelated?, TAggregateRelated> binder) where TAggregateRelated : notnull
        {
            return new BindingBatchedAsyncEnumerable<TValue, TRelated>(
                enumerable,
                () => new ValueBatchAsyncOperation<TValue, TRelated>(joiner),
                (value, fetchedValue, parentValue) => binder((TExistingRelated)parentValue!, (TRelated?)fetchedValue));
        }

        public static IBatchedAsyncEnumerable<TValue, TRelated> JoinByValueAwait<TValue, TExistingRelated, TRelated, TAggregateRelated>(
            this IBatchedAsyncEnumerable<TValue> enumerable,
            Func<IReadOnlyList<TValue>, Task<IReadOnlyList<TRelated?>>> joiner,
            Func<TExistingRelated, TRelated?, TAggregateRelated> binder) where TAggregateRelated : notnull
        {
            return new BindingBatchedAsyncEnumerable<TValue, TRelated>(
                enumerable,
                () => new ValueBatchAsyncOperation<TValue, TRelated>((values, _) => joiner(values)),
                (value, fetchedValue, parentValue) => binder((TExistingRelated)parentValue!, (TRelated?)fetchedValue));
        }
    }
}
