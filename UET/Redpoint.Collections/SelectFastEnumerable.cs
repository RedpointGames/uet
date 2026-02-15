#pragma warning disable CA1849
#pragma warning disable CA1508

namespace Redpoint.Collections
{
    internal sealed class SelectFastEnumerable<TSource, TResult> : IAsyncEnumerable<TResult>
    {
        private readonly IAsyncEnumerable<TSource> _source;
        private readonly Func<TSource, ValueTask<TResult>> _selector;
        private readonly int _maximumParallelisation;

        public SelectFastEnumerable(
            IAsyncEnumerable<TSource> source,
            int maximumParallelisation,
            Func<TSource, ValueTask<TResult>> selector)
        {
            _source = source;
            _selector = selector;
            _maximumParallelisation = maximumParallelisation;
        }

        public IAsyncEnumerator<TResult> GetAsyncEnumerator(
            CancellationToken cancellationToken = default)
        {
            return new SelectFastEnumerator<TSource, TResult>(
                _source.GetAsyncEnumerator(cancellationToken),
                _selector,
                _maximumParallelisation,
                cancellationToken);
        }
    }
}