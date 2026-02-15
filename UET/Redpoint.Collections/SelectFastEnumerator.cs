#pragma warning disable CA1849
#pragma warning disable CA1508

namespace Redpoint.Collections
{
    internal sealed class SelectFastEnumerator<TSource, TResult> : IAsyncEnumerator<TResult>
    {
        private readonly IAsyncEnumerator<TSource> _source;
        private readonly Func<TSource, ValueTask<TResult>> _selector;
        private readonly int _maximumParallelisation;
        private readonly CancellationToken _cancellationToken;
        private readonly List<Task<TResult>> _inFlight;
        private bool _sourceDone;
        private Task<bool>? _movingTask;

        public SelectFastEnumerator(
            IAsyncEnumerator<TSource> source,
            Func<TSource, ValueTask<TResult>> selector,
            int maximumParallelisation,
            CancellationToken cancellationToken)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _selector = selector ?? throw new ArgumentNullException(nameof(selector));
            _maximumParallelisation = maximumParallelisation;
            _cancellationToken = cancellationToken;
            _inFlight = new List<Task<TResult>>();
            _sourceDone = false;
            _movingTask = null;

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
            foreach (var fl in _inFlight)
            {
                if (fl.IsCompleted)
                {
                    _inFlight.Remove(fl);
                    Current = fl.Result;
                    return true;
                }
            }

            if (_movingTask != null)
            {
                var didMove = _movingTask.Result;
                if (_movingTask.Result)
                {
                    var nextValue = _source.Current;
                    _movingTask = null;

                    if (_inFlight.Count == _maximumParallelisation)
                    {
                        // Before we can queue up the selector for the result, we *must*
                        // wait for an existing in flight task to complete.
                        var completedTask = await Task.WhenAny(_inFlight).ConfigureAwait(false);
                        _inFlight.Remove(completedTask);
                        Current = completedTask.Result;

                        // Queue up the obtained value before we return.
                        var capturedValue = _source.Current;
                        _inFlight.Add(Task.Run(() => _selector(capturedValue).AsTask()));

                        return true;
                    }
                    else
                    {
                        // Queue up the obtained value immediately.
                        var capturedValue = _source.Current;
                        _inFlight.Add(Task.Run(() => _selector(capturedValue).AsTask()));

                        // Now fall through to the main logic so we continue filling
                        // up the in-flight buffer with more tasks until we reach
                        // maximum parallelisation.
                    }
                }
                else
                {
                    // Nothing else to get from the source.
                    _sourceDone = true;
                    _movingTask = null;
                }
            }

            while (_inFlight.Count < _maximumParallelisation)
            {
                Task<TResult> completedInFlightTask;

                if (!_sourceDone)
                {
                    // Wait for either an in-flight task to complete or for us to get
                    // the next value from the source.
                    if (_movingTask == null)
                    {
                        _movingTask = _source.MoveNextAsync().AsTask();
                    }
                    var completed = await Task.WhenAny(_inFlight.Cast<Task>().Concat(new[] { _movingTask })).ConfigureAwait(false);
                    if (completed == _movingTask)
                    {
                        // We moved to the next position before any of the current pending
                        // tasks completed.
                        var didMove = _movingTask.Result;
                        _movingTask = null;
                        if (didMove)
                        {
                            // Put the selector into the in-flight queue.
                            var capturedValue = _source.Current;
                            _inFlight.Add(Task.Run(() => _selector(capturedValue).AsTask()));

                            // Continue so that we return for an in-flight task or continue
                            // queuing up more items from the source.
                            continue;
                        }
                        else
                        {
                            // Nothing else to get from the source.
                            _sourceDone = true;

                            // Continue so that we wait on our in-flight tasks.
                            continue;
                        }
                    }
                    else
                    {
                        // The task that was completed was an in-flight task. The "MoveNextAsync"
                        // task is stored in a field, we'll kick off it's selector the next
                        // time that our MoveNextAsync is called.
                        completedInFlightTask = (Task<TResult>)completed;
                        if (completedInFlightTask == null)
                        {
                            throw new InvalidOperationException("Task.WhenAny returned a null task.");
                        }
                    }
                }
                else
                {
                    if (_inFlight.Count == 0)
                    {
                        // The in-flight tasks are finished and there's no more elements in the
                        // source. We are done.
                        return false;
                    }

                    // Just wait out our in-flight tasks.
                    completedInFlightTask = await Task.WhenAny(_inFlight).ConfigureAwait(false);
                    if (completedInFlightTask == null)
                    {
                        throw new InvalidOperationException("Task.WhenAny returned a null task.");
                    }
                }

                if (completedInFlightTask.IsFaulted)
                {
                    if (completedInFlightTask.Exception != null)
                    {
                        throw new AggregateException("An exception occurred within the asynchronous selector.", completedInFlightTask.Exception);
                    }
                    else
                    {
                        throw new AggregateException("An exception occurred within the asynchronous selector.");
                    }
                }

                _inFlight.Remove(completedInFlightTask);
                Current = completedInFlightTask.Result;
                return true;
            }

            // Can't queue up more in-flight tasks due to parallelisation limit. Just
            // wait for the first in-flight task and return it.
            var completedInFlightMaxParallelTask = await Task.WhenAny(_inFlight).ConfigureAwait(false);
            _inFlight.Remove(completedInFlightMaxParallelTask);
            Current = completedInFlightMaxParallelTask.Result;
            return true;
        }
    }
}