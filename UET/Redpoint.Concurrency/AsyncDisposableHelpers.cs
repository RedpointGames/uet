namespace Redpoint.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides a helper function for using <see cref="IAsyncDisposable"/> objects with the <c>await using</c> construct.
    /// </summary>
    public static class AsyncDisposableHelpers
    {
        /// <summary>
        /// Returns a second reference of the object.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="disposable"></param>
        /// <param name="disposableOut"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static IAsyncDisposable AsAsyncDisposable<T>(
            this T disposable,
            out T disposableOut)
            where T : notnull, IAsyncDisposable
            => disposableOut = disposable;
    }
}
