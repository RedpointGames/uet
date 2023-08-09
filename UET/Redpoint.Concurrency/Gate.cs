namespace Redpoint.Concurrency
{
    /// <summary>
    /// Implements a gate, which prevents logic from proceeding until the gate is opened.
    /// </summary>
    public class Gate
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private bool _opened = false;

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

            await _semaphore.WaitAsync(cancellationToken);
            _semaphore.Release();
        }

        /// <summary>
        /// Wait synchronously until the gate is opened.
        /// </summary>
        public void Wait()
        {
            if (_opened)
            {
                return;
            }

            _semaphore.Wait();
            _semaphore.Release();
        }
    }
}