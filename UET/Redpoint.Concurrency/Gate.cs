namespace Redpoint.Concurrency
{
    /// <summary>
    /// Implements a gate, which prevents logic from proceeding until the gate is unlocked.
    /// </summary>
    public class Gate
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(0);
        private bool _unlocked = false;

        /// <summary>
        /// Constructs a new gate in the locked state.
        /// </summary>
        public Gate()
        {
        }

        /// <summary>
        /// Unlocks the gate, allowing code calling <see cref="WaitAsync(CancellationToken)"/> to proceed.
        /// </summary>
        public void Unlock()
        {
            _unlocked = true;
            _semaphore.Release();
        }

        /// <summary>
        /// Returns whether the gate is currently unlocked.
        /// </summary>
        public bool Unlocked => _unlocked;

        /// <summary>
        /// Wait until the gate is unlocked, or the cancellation token is cancelled.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The awaitable task.</returns>
        public async ValueTask WaitAsync(CancellationToken cancellationToken = default)
        {
            if (_unlocked)
            {
                return;
            }

            await _semaphore.WaitAsync(cancellationToken);
            _semaphore.Release();
        }
    }
}