namespace Redpoint.Concurrency
{
    using System;
    using System.ComponentModel;
    using System.Runtime.CompilerServices;
    using System.Runtime.ExceptionServices;

#pragma warning disable CA1034 // Nested types should not be visible
#pragma warning disable CA2012 // Use ValueTasks correctly

    /// <summary>
    /// A simple asynchronous future that can be awaited.
    /// </summary>
    /// <typeparam name="T">The type of value.</typeparam>
    public class Future<T>
    {
        private T? _value;
        private ExceptionDispatchInfo? _exception;
        private readonly Gate _gate;

        [EditorBrowsable(EditorBrowsableState.Never)]
        public class Awaiter : INotifyCompletion
        {
            private readonly Future<T> _future;
            private Action? _continuation;

            internal Awaiter(Future<T> future)
            {
                _future = future;

                var awaiter = future._gate.WaitAsync().GetAwaiter();
                awaiter.OnCompleted(() =>
                {
                    if (_continuation != null)
                    {
                        _continuation();
                    }
                });
            }

            public bool IsCompleted => _future._gate.Opened;

            public T? GetResult()
            {
                if (_future._exception != null)
                {
                    _future._exception.Throw();
                    throw new InvalidOperationException("Expected exception to be thrown.");
                }
                else
                {
                    return _future._value;
                }
            }

            public void OnCompleted(Action continuation)
            {
                ArgumentNullException.ThrowIfNull(continuation);

                if (_future._gate.Opened)
                {
                    continuation();
                }
                else
                {
                    _continuation = continuation;
                }
            }
        }

        public Future()
        {
            _value = default;
            _exception = null;
            _gate = new Gate();
        }

        public bool IsCompleted => _gate.Opened;

        public void SetValue(T value)
        {
            if (_gate.Opened)
            {
                throw new InvalidOperationException("Value has already been set.");
            }

            _value = value;
            _gate.Open();
        }

        public void SetException(Exception ex)
        {
            if (_gate.Opened)
            {
                throw new InvalidOperationException("Value has already been set.");
            }

            _exception = ExceptionDispatchInfo.Capture(ex);
            _gate.Open();
        }

        public Awaiter GetAwaiter()
        {
            return new Awaiter(this);
        }
    }
}

#pragma warning restore CA1034 // Nested types should not be visible
#pragma warning restore CA2012 // Use ValueTasks correctly