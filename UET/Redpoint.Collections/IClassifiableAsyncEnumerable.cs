namespace Redpoint.Collections
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    /// <summary>
    /// Returned by extension methods in <see cref="ClassifyingLinqExtensions"/>, these methods allow you to set the behaviour for each element in the enumeration based on how they were classified.
    /// </summary>
    /// <typeparam name="TIn">The type element of the original enumeration.</typeparam>
    /// <typeparam name="TOut">The output of the enumeration.</typeparam>
    public interface IClassifiableAsyncEnumerable<TIn, TOut> : IAsyncEnumerable<TOut>
    {
        /// <summary>
        /// When classified as <c>classification</c>, performs the synchronous mapping specified by <c>handler</c>.
        /// </summary>
        /// <param name="classification">The classification that this mapping will run for.</param>
        /// <param name="handler">The <c>Select</c> equivalent mapping function.</param>
        /// <returns>The current instance, so you can chain more classifiers.</returns>
        IClassifiableAsyncEnumerable<TIn, TOut> AndForClassification(string classification, Func<TIn, TOut> handler);

        /// <summary>
        /// When classified as <c>classification</c>, performs the asynchronous mapping specified by <c>handler</c>.
        /// </summary>
        /// <param name="classification">The classification that this mapping will run for.</param>
        /// <param name="handler">The <c>SelectAwait</c> equivalent mapping function.</param>
        /// <returns>The current instance, so you can chain more classifiers.</returns>
        IClassifiableAsyncEnumerable<TIn, TOut> AndForClassificationAwait(string classification, Func<TIn, Task<TOut>> handler);

        /// <summary>
        /// For all the elements classified as <c>classification</c>, calls the <c>handler</c> once to map all the elements of that classification asynchronously.
        /// </summary>
        /// <param name="classification">Elements of this classification will be passed as an asynchronous stream into the handler.</param>
        /// <param name="handler">The handler which asynchronously processes elements.</param>
        /// <returns>The current instance, so you can chain more classifiers.</returns>
        IClassifiableAsyncEnumerable<TIn, TOut> AndForClassificationStream(string classification, Func<IAsyncEnumerable<TIn>, IAsyncEnumerable<TOut>> handler);
    }
}
