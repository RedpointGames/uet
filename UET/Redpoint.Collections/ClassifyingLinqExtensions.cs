namespace Redpoint.Collections
{
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;

    /// <summary>
    /// Provides extension methods to <see cref="IAsyncEnumerable{T}"/> that allow you to classify elements in an enumeration.
    /// </summary>
    public static class ClassifyingLinqExtensions
    {
        /// <summary>
        /// Classifies the elements in the asynchronous <c>enumerable</c> using the specified <c>classifier</c>.
        /// 
        /// The resulting enumerable returns elements in the order that the processors return
        /// them to ensure the fastest iteration possible. Order is not preserved.
        /// </summary>
        public static IClassifiableAsyncEnumerable<T, T> Classify<T>(this IAsyncEnumerable<T> enumerable, Func<T, string> classifier)
        {
            return new ClassifyingEnumerable<T, T>(enumerable, x => Task.FromResult(classifier(x)));
        }

        /// <summary>
        /// Classifies the elements in the asynchronous <c>enumerable</c> using the specified <c>classifier</c>.
        /// 
        /// The resulting enumerable returns elements in the order that the processors return
        /// them to ensure the fastest iteration possible. Order is not preserved.
        /// </summary>
        public static IClassifiableAsyncEnumerable<T, T> ClassifyAwait<T>(this IAsyncEnumerable<T> enumerable, Func<T, Task<string>> classifier)
        {
            return new ClassifyingEnumerable<T, T>(enumerable, classifier);
        }

        /// <summary>
        /// Classifies the elements in the asynchronous <c>enumerable</c> using the specified <c>classifier</c>.
        /// 
        /// The resulting enumerable returns elements in the order that the processors return
        /// them to ensure the fastest iteration possible. Order is not preserved.
        /// </summary>
        public static IClassifiableAsyncEnumerable<TIn, TOut> Classify<TIn, TOut>(this IAsyncEnumerable<TIn> enumerable, Func<TIn, string> classifier)
        {
            return new ClassifyingEnumerable<TIn, TOut>(enumerable, x => Task.FromResult(classifier(x)));
        }

        /// <summary>
        /// Classifies the elements in the asynchronous <c>enumerable</c> using the specified <c>classifier</c>.
        /// 
        /// The resulting enumerable returns elements in the order that the processors return
        /// them to ensure the fastest iteration possible. Order is not preserved.
        /// </summary>
        public static IClassifiableAsyncEnumerable<TIn, TOut> ClassifyAwait<TIn, TOut>(this IAsyncEnumerable<TIn> enumerable, Func<TIn, Task<string>> classifier)
        {
            return new ClassifyingEnumerable<TIn, TOut>(enumerable, classifier);
        }

        private sealed class ClassifyingEnumerable<TIn, TOut> : IClassifiableAsyncEnumerable<TIn, TOut>
        {
            private readonly IAsyncEnumerable<TIn> _source;
            private readonly Func<TIn, Task<string>> _classifier;
            private readonly Dictionary<string, Func<IAsyncEnumerable<TIn>, IAsyncEnumerable<TOut>>> _connections;

            public ClassifyingEnumerable(
                IAsyncEnumerable<TIn> source,
                Func<TIn, Task<string>> classifier)
            {
                _source = source;
                _classifier = classifier;
                _connections = new Dictionary<string, Func<IAsyncEnumerable<TIn>, IAsyncEnumerable<TOut>>>();
            }

            private static async IAsyncEnumerable<TOut> WrapMapper(IAsyncEnumerable<TIn> inputs, Func<TIn, TOut> handler)
            {
                await foreach (var input in inputs)
                {
                    yield return handler(input);
                }
            }

            private static async IAsyncEnumerable<TOut> WrapMapper(IAsyncEnumerable<TIn> inputs, Func<TIn, Task<TOut>> handler)
            {
                await foreach (var input in inputs)
                {
                    yield return await handler(input).ConfigureAwait(false);
                }
            }

            public IClassifiableAsyncEnumerable<TIn, TOut> AndForClassification(string classification, Func<TIn, TOut> handler)
            {
                _connections[classification] = inputs => ClassifyingEnumerable<TIn, TOut>.WrapMapper(inputs, handler);
                return this;
            }

            public IClassifiableAsyncEnumerable<TIn, TOut> AndForClassificationAwait(string classification, Func<TIn, Task<TOut>> handler)
            {
                _connections[classification] = inputs => ClassifyingEnumerable<TIn, TOut>.WrapMapper(inputs, handler);
                return this;
            }

            public IClassifiableAsyncEnumerable<TIn, TOut> AndForClassificationStream(string classification, Func<IAsyncEnumerable<TIn>, IAsyncEnumerable<TOut>> handler)
            {
                _connections[classification] = handler;
                return this;
            }

            private static async Task<(IAsyncEnumerator<T> enumerator, bool moved, T? current)> MoveNextAndGetCurrentAsync<T>(IAsyncEnumerator<T> enumerator)
            {
                var moved = await enumerator.MoveNextAsync().ConfigureAwait(false);
                if (moved)
                {
                    return (enumerator, true, enumerator.Current);
                }
                else
                {
                    return (enumerator, false, default(T));
                }
            }

            private async IAsyncEnumerable<TOut> Enumerate([EnumeratorCancellation] CancellationToken cancellationToken)
            {
                var inputEnumerator = new ClassifyingInputEnumerator<TIn>(_source.GetAsyncEnumerator(cancellationToken), _classifier);
                var enumerators = _connections.Select(kv =>
                {
                    var connectionEnumerable = new ClassifyingOutputEnumerable<TIn>(kv.Key, inputEnumerator);
                    return kv.Value(connectionEnumerable).GetAsyncEnumerator(cancellationToken);
                }).ToDictionary(k => k, v => ClassifyingEnumerable<TIn, TOut>.MoveNextAndGetCurrentAsync(v));
                while (enumerators.Count > 0)
                {
                    var nextValue = await (await Task.WhenAny(enumerators.Values).ConfigureAwait(false)).ConfigureAwait(false);
                    if (!nextValue.moved)
                    {
                        // This enumerator is done.
                        enumerators.Remove(nextValue.enumerator);
                    }
                    else
                    {
                        yield return nextValue.current;
                        enumerators[nextValue.enumerator] = ClassifyingEnumerable<TIn, TOut>.MoveNextAndGetCurrentAsync(nextValue.enumerator);
                    }
                }
            }

            public IAsyncEnumerator<TOut> GetAsyncEnumerator(CancellationToken cancellationToken = default)
            {
                return Enumerate(cancellationToken).GetAsyncEnumerator(cancellationToken);
            }

            private sealed class ClassifyingInputEnumerator<T>
            {
                private readonly IAsyncEnumerator<T> _source;
                private readonly Func<T, Task<string>> _classifier;
                private Semaphore _iterationMutex;
                private Dictionary<string, Queue<T>> _buffers;
                private bool _done;

                public ClassifyingInputEnumerator(IAsyncEnumerator<T> source, Func<T, Task<string>> classifier)
                {
                    _iterationMutex = new Semaphore(1);
                    _buffers = new Dictionary<string, Queue<T>>();
                    _source = source;
                    _classifier = classifier;
                    _done = false;
                }

                public async ValueTask<(bool moved, T next)> GetNextAsync(string classification, CancellationToken cancellationToken)
                {
                    await _iterationMutex.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        if (_buffers.TryGetValue(classification, out Queue<T>? earlyValue))
                        {
                            if (earlyValue.Count > 0)
                            {
                                return (true, earlyValue.Dequeue());
                            }
                        }

                        if (_done)
                        {
                            return (false, default(T)!);
                        }

                        while (true)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!await _source.MoveNextAsync().ConfigureAwait(false))
                            {
                                // Enumeration is complete.
                                _done = true;
                                return (false, default(T)!);
                            }
                            var nextClassification = await _classifier(_source.Current).ConfigureAwait(false);
                            if (nextClassification == classification)
                            {
                                return (true, _source.Current);
                            }
                            else
                            {
                                if (!_buffers.TryGetValue(nextClassification, out Queue<T>? newValue))
                                {
                                    newValue = new Queue<T>();
                                    _buffers[nextClassification] = newValue;
                                }

                                newValue.Enqueue(_source.Current);

                                // Try to get another item that matches the classification the caller wants.
                                continue;
                            }
                        }
                    }
                    finally
                    {
                        _iterationMutex.Release();
                    }
                }
            }

            private sealed class ClassifyingOutputEnumerator<T> : IAsyncEnumerator<T>
            {
                private readonly string _classification;
                private readonly ClassifyingInputEnumerator<T> _enumerator;
                private readonly CancellationToken _cancellationToken;

                public ClassifyingOutputEnumerator(
                    string classification,
                    ClassifyingInputEnumerator<T> enumerator,
                    CancellationToken cancellationToken)
                {
                    _classification = classification;
                    _enumerator = enumerator;
                    _cancellationToken = cancellationToken;

                    Current = default(T)!;
                }

                public T Current { get; private set; }

                public ValueTask DisposeAsync()
                {
                    return ValueTask.CompletedTask;
                }

                public async ValueTask<bool> MoveNextAsync()
                {
                    var (moved, current) = await _enumerator.GetNextAsync(_classification, _cancellationToken).ConfigureAwait(false);
                    Current = current;
                    return moved;
                }
            }

            private sealed class ClassifyingOutputEnumerable<T> : IAsyncEnumerable<T>
            {
                private readonly string _classification;
                private readonly ClassifyingInputEnumerator<T> _enumerator;

                public ClassifyingOutputEnumerable(
                    string classification,
                    ClassifyingInputEnumerator<T> enumerator)
                {
                    _classification = classification;
                    _enumerator = enumerator;
                }

                public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                {
                    return new ClassifyingOutputEnumerator<T>(_classification, _enumerator, cancellationToken);
                }
            }
        }
    }

}
