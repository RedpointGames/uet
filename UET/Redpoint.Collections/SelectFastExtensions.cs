#pragma warning disable CA1849
#pragma warning disable CA1508

namespace Redpoint.Collections
{
    /// <summary>
    /// Provides extension methods to <see cref="IAsyncEnumerable{T}"/> that allow you to select values in parallel.
    /// </summary>
    public static class SelectFastExtensions
    {
        /// <summary>
        /// Select elements from the asynchronous enumerable, in parallel across (core count - 1) processes. Results may be emitted
        /// in any order, depending on when the selector returns values.
        /// </summary>
        /// <typeparam name="TSource">The source type of the original enumerable.</typeparam>
        /// <typeparam name="TResult">The output type of the enumerable.</typeparam>
        /// <param name="enumerable">The enumerable to select elements from.</param>
        /// <param name="selector">The selector to run in parallel over the received elements.</param>
        /// <returns>A new asynchronously enumerable that returns the results as soon as they are returned by the selector.</returns>
        public static IAsyncEnumerable<TResult> SelectFast<TSource, TResult>(
            this IAsyncEnumerable<TSource> enumerable,
            Func<TSource, ValueTask<TResult>> selector)
        {
            return new SelectFastEnumerable<TSource, TResult>(
                enumerable,
                Math.Max(1, Environment.ProcessorCount - 1),
                selector);
        }

        /// <summary>
        /// Select many elements from the asynchronous enumerable, in parallel across (core count - 1) processes. Results may be emitted
        /// in any order, depending on when the selector returns values.
        /// </summary>
        /// <typeparam name="TSource">The source type of the original enumerable.</typeparam>
        /// <typeparam name="TResult">The output type of the enumerable.</typeparam>
        /// <param name="enumerable">The enumerable to select elements from.</param>
        /// <param name="selector">The selector to run in parallel over the received elements.</param>
        /// <returns>A new asynchronously enumerable that returns the results as soon as they are returned by the selector.</returns>
        public static IAsyncEnumerable<TResult> SelectManyFast<TSource, TResult>(
            this IAsyncEnumerable<TSource> enumerable,
            Func<TSource, IAsyncEnumerable<TResult>> selector)
        {
            return new SelectManyFastEnumerable<TSource, TResult>(
                enumerable,
                Math.Max(1, Environment.ProcessorCount - 1),
                selector);
        }

        /// <summary>
        /// Select elements from the asynchronous enumerable, in parallel across <c>maximumParallelisation</c> processes. Results may be emitted
        /// in any order, depending on when the selector returns values.
        /// </summary>
        /// <typeparam name="TSource">The source type of the original enumerable.</typeparam>
        /// <typeparam name="TResult">The output type of the enumerable.</typeparam>
        /// <param name="enumerable">The enumerable to select elements from.</param>
        /// <param name="maximumParallelisation">The maximum number of tasks to run in parallel for this selection.</param>
        /// <param name="selector">The selector to run in parallel over the received elements.</param>
        /// <returns>A new asynchronously enumerable that returns the results as soon as they are returned by the selector.</returns>
        public static IAsyncEnumerable<TResult> SelectFast<TSource, TResult>(
            this IAsyncEnumerable<TSource> enumerable,
            int maximumParallelisation,
            Func<TSource, ValueTask<TResult>> selector)
        {
            return new SelectFastEnumerable<TSource, TResult>(
                enumerable,
                maximumParallelisation,
                selector);
        }

        /// <summary>
        /// Select many elements from the asynchronous enumerable, in parallel across <c>maximumParallelisation</c> processes. Results may be emitted
        /// in any order, depending on when the selector returns values.
        /// </summary>
        /// <typeparam name="TSource">The source type of the original enumerable.</typeparam>
        /// <typeparam name="TResult">The output type of the enumerable.</typeparam>
        /// <param name="enumerable">The enumerable to select elements from.</param>
        /// <param name="maximumParallelisation">The maximum number of tasks to run in parallel for this selection.</param>
        /// <param name="selector">The selector to run in parallel over the received elements.</param>
        /// <returns>A new asynchronously enumerable that returns the results as soon as they are returned by the selector.</returns>
        public static IAsyncEnumerable<TResult> SelectManyFast<TSource, TResult>(
            this IAsyncEnumerable<TSource> enumerable,
            int maximumParallelisation,
            Func<TSource, IAsyncEnumerable<TResult>> selector)
        {
            return new SelectManyFastEnumerable<TSource, TResult>(
                enumerable,
                maximumParallelisation,
                selector);
        }

        [Obsolete("Use SelectFast instead.")]
        public static IAsyncEnumerable<TResult> SelectFastAwait<TSource, TResult>(
            this IAsyncEnumerable<TSource> enumerable,
            Func<TSource, ValueTask<TResult>> selector)
        {
            return SelectFast<TSource, TResult>(enumerable, selector);
        }

        [Obsolete("Use SelectFast instead.")]
        public static IAsyncEnumerable<TResult> SelectFastAwait<TSource, TResult>(
            this IAsyncEnumerable<TSource> enumerable,
            int maximumParallelisation,
            Func<TSource, ValueTask<TResult>> selector)
        {
            return SelectFast<TSource, TResult>(enumerable, maximumParallelisation, selector);
        }
    }
}