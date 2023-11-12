namespace Redpoint.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides extension methods to <see cref="IAsyncEnumerable{T}"/> and <see cref="IEnumerable{T}"/> that batch enumerations into chunks of a given size.
    /// </summary>
    public static class BatchingExtensions
    {
        /// <summary>
        /// Batches the given enumerable into the given batch size.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="enumerable">The enumerable to batch.</param>
        /// <param name="batchSize">The maximum size of each batch.</param>
        /// <returns>An enumerable of batches.</returns>
        public static IEnumerable<IReadOnlyList<T>> BatchInto<T>(this IEnumerable<T> enumerable, int batchSize)
        {
            ArgumentNullException.ThrowIfNull(enumerable);

            var buffer = new List<T>();
            foreach (var item in enumerable)
            {
                buffer.Add(item);
                if (buffer.Count >= batchSize)
                {
                    yield return buffer;
                    buffer = new List<T>();
                }
            }
            if (buffer.Count > 0)
            {
                yield return buffer;
            }
        }

        /// <summary>
        /// Batches the given asynchronous enumerable into the given batch size.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="enumerable">The enumerable to batch.</param>
        /// <param name="batchSize">The maximum size of each batch.</param>
        /// <returns>An enumerable of batches.</returns>
        public static async IAsyncEnumerable<IReadOnlyList<T>> BatchInto<T>(this IAsyncEnumerable<T> enumerable, int batchSize)
        {
            ArgumentNullException.ThrowIfNull(enumerable);

            var buffer = new List<T>();
            await foreach (var item in enumerable)
            {
                buffer.Add(item);
                if (buffer.Count >= batchSize)
                {
                    yield return buffer;
                    buffer = new List<T>();
                }
            }
            if (buffer.Count > 0)
            {
                yield return buffer;
            }
        }
    }
}
