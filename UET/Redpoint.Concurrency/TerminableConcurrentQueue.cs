namespace Redpoint.Concurrency
{
    using System;
    using System.Collections;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;

    /// <summary>
    /// A version of <see cref="ConcurrentQueue{T}"/> that you can terminate
    /// downstream enumerations.
    /// </summary>
    /// <typeparam name="T">The element in the queue.</typeparam>
    [SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "This class implements a queue.")]
    public class TerminableConcurrentQueue<T> : IEnumerable<T>
    {
        private readonly Semaphore _ready = new Semaphore(0);
        private readonly ConcurrentQueue<T> _queue = new ConcurrentQueue<T>();
        private bool _terminated;

        /// <summary>
        /// Enqueue an item into the queue.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void Enqueue(T item)
        {
            if (_terminated)
            {
                throw new InvalidOperationException("This concurrent queue has been terminated.");
            }
            _queue.Enqueue(item);
            _ready.Release();
        }

        /// <summary>
        /// Terminates the queue, meaning that no further items can be dequeued
        /// from it. Once this is called, <see cref="Dequeue(CancellationToken)"/>
        /// will throw <see cref="OperationCanceledException"/>, and enumerables from
        /// <see cref="GetEnumerator()"/> will stop enumerating
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
        /// <returns>The item that was dequeued.</returns>
        public T Dequeue(CancellationToken cancellationToken)
        {
            _ready.Wait(cancellationToken);
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
        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        private sealed class Enumerator : IEnumerator<T>
        {
            private readonly TerminableConcurrentQueue<T> _queue;
            private T? _current;
            private bool _currentSet;

            public Enumerator(TerminableConcurrentQueue<T> queue)
            {
                _queue = queue;
                _current = default(T);
                _currentSet = false;
            }

            public T Current => _currentSet switch
            {
                true => _current!,
                false => throw new InvalidOperationException("You must call MoveNext first!"),
            };

            object IEnumerator.Current => Current!;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                _queue._ready.Wait(CancellationToken.None);
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

            public void Reset()
            {
            }
        }
    }
}
