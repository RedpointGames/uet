namespace Redpoint.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;

    /// <summary>
    /// Extension methods for <see cref="List{T}"/>.
    /// </summary>
    public static class ListExtensions
    {
        /// <summary>
        /// Shuffle the list randomly.
        /// </summary>
        /// <typeparam name="T">The list element type.</typeparam>
        /// <param name="list">The list to shuffle.</param>
        [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Shuffle() is not intended to be used in security-related scenarios.")]
        public static void Shuffle<T>(this List<T> list)
        {
            ArgumentNullException.ThrowIfNull(list);

            for (var i = list.Count - 1; i > 0; i--)
            {
                var k = Random.Shared.Next(i + 1);
                var v = list[k];
                list[k] = list[i];
                list[i] = v;
            }
        }
    }
}
