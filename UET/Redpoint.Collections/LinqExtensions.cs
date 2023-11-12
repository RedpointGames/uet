namespace Redpoint.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides extension methods to <see cref="IAsyncEnumerable{T}"/> and <see cref="IEnumerable{T}"/>.
    /// </summary>
    public static class LinqExtensions
    {
        /// <summary>
        /// Converts the enumerable elements to a dictionary, ignoring duplicate keys.
        /// </summary>
        /// <typeparam name="TElement">The source enumeration element type.</typeparam>
        /// <typeparam name="TKey">The key type for the dictionary.</typeparam>
        /// <typeparam name="TValue">The value type for the dictionary.</typeparam>
        /// <param name="source">The source enumeration.</param>
        /// <param name="keySelector">The function that selects the key from the source element.</param>
        /// <param name="valueSelector">The function that selects the value from the source element.</param>
        /// <param name="cancellationToken">The cancellation token to cancel enumeration.</param>
        /// <returns>The dictionary generated from the source enumeration.</returns>
        public async static ValueTask<Dictionary<TKey, TValue>> ToSafeDictionaryAsync<TElement, TKey, TValue>(
            this IAsyncEnumerable<TElement> source,
            Func<TElement, TKey> keySelector, 
            Func<TElement, TValue> valueSelector,
            CancellationToken cancellationToken = default) where TKey : notnull
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(keySelector);
            ArgumentNullException.ThrowIfNull(valueSelector);

            var result = new Dictionary<TKey, TValue>();
            await foreach (var kv in source.WithCancellation(cancellationToken))
            {
                var key = keySelector(kv);
                if (!result.ContainsKey(key))
                {
                    result.Add(key, valueSelector(kv));
                }
            }
            return result;
        }

        /// <summary>
        /// Filters null elements out of an enumeration and returns an enumeration of non-nullable values.
        /// </summary>
        /// <typeparam name="T">The element type of the source enumeration.</typeparam>
        /// <param name="source">The source enumeration.</param>
        /// <returns>The enumerable of non-nullable values.</returns>
        public static IEnumerable<T> WhereNotNull<T>(this IEnumerable<T?> source)
        {
            return source.Where(x => x != null)!;
        }

        /// <summary>
        /// Filters null elements out of an asynchronous enumeration and returns an asynchronous enumeration of non-nullable values.
        /// </summary>
        /// <typeparam name="T">The element type of the source enumeration.</typeparam>
        /// <param name="source">The source enumeration.</param>
        /// <returns>The asynchronous enumerable of non-nullable values.</returns>
        public static IAsyncEnumerable<T> WhereNotNull<T>(this IAsyncEnumerable<T?> source)
        {
            return source.Where(x => x != null)!;
        }
    }
}
