#pragma warning disable CA1849
#pragma warning disable CA1508

namespace Redpoint.Collections
{
    internal sealed class SelectManyFastEnumerable<TSource, TResult> : IAsyncEnumerable<TResult>
    {
        private readonly IAsyncEnumerable<TSource> _source;
        private readonly Func<TSource, IAsyncEnumerable<TResult>> _selector;
        private readonly int _maximumParallelisation;

        public SelectManyFastEnumerable(
            IAsyncEnumerable<TSource> source,
            int maximumParallelisation,
            Func<TSource, IAsyncEnumerable<TResult>> selector)
        {
            _source = source;
            _selector = selector;
            _maximumParallelisation = maximumParallelisation;
        }

        public IAsyncEnumerator<TResult> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            return new SelectManyFastEnumerator<TSource, TResult>(
                _source.GetAsyncEnumerator(cancellationToken),
                _selector,
                _maximumParallelisation,
                cancellationToken);
        }
    }
}