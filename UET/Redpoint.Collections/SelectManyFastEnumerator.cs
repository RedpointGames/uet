#pragma warning disable CA1849
#pragma warning disable CA1508

namespace Redpoint.Collections
{
    using Redpoint.Concurrency;
    using System.Diagnostics;

    internal sealed class SelectManyFastEnumerator<TSource, TResult> : IAsyncEnumerator<TResult>
    {
        private readonly IAsyncEnumerator<TSource> _source;
        private readonly Func<TSource, IAsyncEnumerable<TResult>> _selector;
        private readonly int _maximumParallelisation;
        private readonly CancellationToken _cancellationToken;
        private readonly List<AsyncEnumeratorState> _inFlight;
        private readonly Mutex _inFlightMutex;
        private bool _sourceDone;
        private Task<bool>? _sourceMoveTask;

        private class AsyncEnumeratorState
        {
            public required IAsyncEnumerator<TResult> Enumerator { get; set; }

            public Task<bool>? Current { get; set; }

            public bool NeedsMove { get; set; } = true;

            public bool EndOfEnumerator { get; set; } = false;
        }

        public SelectManyFastEnumerator(
            IAsyncEnumerator<TSource> source,
            Func<TSource, IAsyncEnumerable<TResult>> selector,
            int maximumParallelisation,
            CancellationToken cancellationToken)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _maximumParallelisation = maximumParallelisation;
            _cancellationToken = cancellationToken;
            _inFlight = new();
            _inFlightMutex = new();
            _sourceDone = false;
            _sourceMoveTask = null;

            if (_maximumParallelisation == 0)
            {
                throw new ArgumentException("Parallelisation must be 1 or greater.", nameof(maximumParallelisation));
            }

            Current = default(TResult)!;
        }

        public TResult Current { get; private set; }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        public async ValueTask<bool> MoveNextAsync()
        {
        retryPoll:
            // We use a mutex here to ensure there's no window between checking EndOfEnumerator
            // and accessing the task result.
            var didSetCurrent = false;
            using (await _inFlightMutex.WaitAsync(_cancellationToken))
            {
                // Remove any enumerators that have reached the end.
                for (int i = _inFlight.Count - 1; i >= 0; i--)
                {
                    if (_inFlight[i].EndOfEnumerator)
                    {
                        _inFlight.RemoveAt(i);
                    }
                }

                // If we have no more in-flight tasks, and we're at the end of the source
                // then we have no more values.
                if (_inFlight.Count == 0 && _sourceDone)
                {
                    return false;
                }

                // See if any enumerators have a result for us.
                foreach (var fl in _inFlight)
                {
                    if (fl.Current != null && fl.Current.IsCompleted && !fl.NeedsMove && fl.Current.Result)
                    {
                        Current = fl.Enumerator.Current;
                        fl.NeedsMove = true;
                        didSetCurrent = true;

                        // Break out of this loop, but let us schedule more tasks now that this
                        // one is no longer doing work. This ensures enumerators are trying to
                        // get the next value while the consumer of this enumeration is doing
                        // work on the current value.
                        break;
                    }
                }
            }

            // Check if the moving task is ready.
            if (_sourceMoveTask != null && _sourceMoveTask.IsCompleted)
            {
                var didSourceMove = _sourceMoveTask.Result;
                if (didSourceMove)
                {
                    var nextEnumerable = _selector(_source.Current);

                    // Store the next enumerator state.
                    var enumeratorState = new AsyncEnumeratorState
                    {
                        Enumerator = nextEnumerable.GetAsyncEnumerator(_cancellationToken),
                    };
                    _inFlight.Add(enumeratorState);

                    // Start another moving operation.
                    _sourceMoveTask = _source.MoveNextAsync().AsTask();
                }
                else
                {
                    // Nothing else to get from the source.
                    _sourceDone = true;
                    _sourceMoveTask = null;
                }
            }
            else if (_sourceMoveTask == null && !_sourceDone)
            {
                // Start the first moving operation.
                _sourceMoveTask = _source.MoveNextAsync().AsTask();
            }

            // Calculate the number of enumeration MoveNextAsync tasks to start.
            var availableCapacity = _maximumParallelisation
                - _inFlight.Count(x => x.Current != null && !x.Current.IsCompleted)
                - ((_sourceMoveTask != null && !_sourceMoveTask.IsCompleted) ? 1 : 0);
            foreach (var inFlight in _inFlight)
            {
                if (availableCapacity <= 0)
                {
                    break;
                }
                if (!inFlight.NeedsMove)
                {
                    continue;
                }

                // We have available capacity.
                inFlight.NeedsMove = false;
                inFlight.Current = Task.Run(
                    async () =>
                    {
                        if (await inFlight.Enumerator.MoveNextAsync())
                        {
                            return true;
                        }
                        else
                        {
                            // We're at the end of this enumerator.
                            using (await _inFlightMutex.WaitAsync(_cancellationToken))
                            {
                                inFlight.EndOfEnumerator = true;
                            }

                            // We have no more data, and Current isn't set on the enumerator.
                            return false;
                        }
                    },
                    CancellationToken.None);
                availableCapacity--;
            }

            // If we got a result earlier, we now return true.
            if (didSetCurrent)
            {
                return true;
            }

            // Otherwise, await for the next available task (whether that's the source enumerator, or
            // any of the mapped enumerators).
            var tasksToAwait = new List<Task>();
            if (_sourceMoveTask != null)
            {
                tasksToAwait.Add(_sourceMoveTask);
            }
            foreach (var inFlight in _inFlight)
            {
                if (inFlight.Current != null && !inFlight.EndOfEnumerator)
                {
                    tasksToAwait.Add(inFlight.Current);
                }
            }
            if (tasksToAwait.Count > 0)
            {
                await Task.WhenAny(tasksToAwait);
            }
            goto retryPoll;
        }
    }
}