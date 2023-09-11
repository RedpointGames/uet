namespace Redpoint.Concurrency
{
    /// <summary>
    /// Implements a gate, which prevents logic from proceeding until the gate is opened.
    /// </summary>
    public class Gate
    {
        private readonly Semaphore _semaphore = new Semaphore(0);
        private bool _opened;

        /// <summary>
        /// Constructs a new gate in the closed state.
        /// </summary>
        public Gate()
        {
        }

        /// <summary>
        /// Opens the gate, allowing code calling <see cref="WaitAsync(CancellationToken)"/> to proceed.
        /// </summary>
        [Obsolete("Use Open() instead.")]
        public void Unlock()
        {
            Open();
        }

        /// <summary>
        /// Opens the gate, allowing code calling <see cref="WaitAsync(CancellationToken)"/> to proceed.
        /// </summary>
        public void Open()
        {
            _opened = true;
            _semaphore.Release();
        }

        /// <summary>
        /// Returns whether the gate is currently opened.
        /// </summary>
        [Obsolete("Use Opened instead.")]
        public bool Unlocked => _opened;

        /// <summary>
        /// Returns whether the gate is currently opened.
        /// </summary>
        public bool Opened => _opened;

        /// <summary>
        /// Wait until the gate is opened, or the cancellation token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The awaitable task.</returns>
        public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
        {
            if (_opened)
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            _semaphore.Release();
        }

        /// <summary>
        /// Wait synchronously until the gate is opened.
        /// </summary>
        public void Wait(CancellationToken cancellationToken)
        {
            if (_opened)
            {
                return;
            }

            _semaphore.Wait(cancellationToken);
            _semaphore.Release();
        }

        /// <summary>
        /// Wait synchronously until the gate is opened or the elapsed milliseconds have passed.
        /// </summary>
        public bool TryWait(int elapsedMilliseconds, CancellationToken cancellationToken)
        {
            if (_opened)
            {
                return true;
            }

            if (!_semaphore.Wait(elapsedMilliseconds, cancellationToken))
            {
                return false;
            }
            _semaphore.Release();
            return true;
        }
    }
}