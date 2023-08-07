namespace Redpoint.Concurrency
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A version of <see cref="AwaitableConcurrentQueue{T}"/> that you can terminate
    /// downstream enumerations.
    /// </summary>
    /// <typeparam name="T">The element in the queue.</typeparam>
    public class TerminableAwaitableConcurrentQueue<T> : IAsyncEnumerable<T>
    {
        private readonly SemaphoreSlim _ready = new SemaphoreSlim(0);
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private bool _terminated = false;

        /// <summary>
        /// Enqueue an item into the queue.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void Enqueue(T item)
        {
            if (_terminated)
            {
                throw new InvalidOperationException("This asynchronous concurrent queue has been terminated.");
            }
            _queue.Enqueue(item);
            _ready.Release();
        }

        /// <summary>
        /// Terminates the queue, meaning that no further items can be dequeued
        /// from it. Once this is called, <see cref="Dequeue(CancellationToken)"/>
        /// will throw <see cref="OperationCanceledException"/>, and enumerables from
        /// <see cref="GetAsyncEnumerator(CancellationToken)"/> will stop enumerating
        /// normally.
        /// </summary>
        public void Terminate()
        {
            _terminated = true;
            _ready.Release();
        }

        /// <summary>
        /// The number of items in the queue. This property is not thread-safe
        /// and may return a slightly different value if an item is being added
        /// or removed from the queue at the same time.
        /// </summary>
        public int Count => _queue.Count;

        /// <summary>
        /// Dequeues an item from the queue, asynchronously waiting until an item
        /// is available.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token to cancel the dequeue operation.</param>
        /// <returns>The item that was dequeued.</returns>
        public async ValueTask<T> Dequeue(CancellationToken cancellationToken)
        {
            await _ready.WaitAsync(cancellationToken);
            if (!_queue.TryDequeue(out var result))
            {
                if (_terminated)
                {
                    _ready.Release();
                    throw new OperationCanceledException("This asynchronous concurrent queue has been terminated.");
                }
                else
                {
                    throw new InvalidOperationException("Dequeue failed to pull item off queue. This is an internal bug.");
                }
            }
            return result!;
        }

        /// <inheritdoc />
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            return new AsyncEnumerator(this, cancellationToken);
        }

        private class AsyncEnumerator : IAsyncEnumerator<T>
        {
            private readonly TerminableAwaitableConcurrentQueue<T> _queue;
            private readonly CancellationToken _cancellationToken;
            private T? _current;
            private bool _currentSet;

            public AsyncEnumerator(TerminableAwaitableConcurrentQueue<T> queue, CancellationToken cancellationToken)
            {
                _queue = queue;
                _cancellationToken = cancellationToken;
                _current = default(T);
                _currentSet = false;
            }

            public T Current => _currentSet switch
            {
                true => _current!,
                false => throw new InvalidOperationException("You must call MoveNext first!"),
            };

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }

            public async ValueTask<bool> MoveNextAsync()
            {
                await _queue._ready.WaitAsync(_cancellationToken);
                if (!_queue._queue.TryDequeue(out var result))
                {
                    if (_queue._terminated)
                    {
                        _queue._ready.Release();
                        return false;
                    }
                    else
                    {
                        throw new InvalidOperationException("Dequeue failed to pull item off queue. This is an internal bug.");
                    }
                }
                _current = result!;
                _currentSet = true;
                return true;
            }
        }
    }
}
