namespace Redpoint.Concurrency
{
    using System.Diagnostics.CodeAnalysis;
    using System.Threading;

    /// <summary>
    /// A wrapper around <see cref="SemaphoreSlim"/> that does not require calling
    /// <see cref="SemaphoreSlim.Dispose()"/> because it does not expose an unmanaged
    /// wait handle. This implementation also prevents you from ever waiting without a cancellation token;
    /// if you really want to wait with no possibility of cancellation, use <see cref="CancellationToken.None"/>.
    /// </summary>
    [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "This type does not expose the underlying AvailableWaitHandle, so no unmanaged resources will ever need to be disposed via a Dispose() call.")]
    public class Semaphore
    {
        private readonly SemaphoreSlim _internalSemaphore;

        /// <summary>
        /// Initializes a new instance of the <see cref="Semaphore"/> class, specifying
        /// the initial number of requests that can be granted concurrently.
        /// </summary>
        /// <param name="initialCount">The initial number of requests for the semaphore that can be granted
        /// concurrently.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="initialCount"/>
        /// is less than 0.</exception>
        public Semaphore(int initialCount)
        {
            _internalSemaphore = new SemaphoreSlim(initialCount);
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="Semaphore"/>, while observing a
        /// <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> token to
        /// observe.</param>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was
        /// canceled.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been
        /// disposed.</exception>
        public void Wait(CancellationToken cancellationToken)
        {
            _internalSemaphore.Wait(cancellationToken);
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="Semaphore"/>, using a <see
        /// cref="TimeSpan"/> to measure the time interval, while observing a <see
        /// cref="CancellationToken"/>.
        /// </summary>
        /// <param name="timeout">A <see cref="TimeSpan"/> that represents the number of milliseconds
        /// to wait, or a <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to
        /// observe.</param>
        /// <returns>true if the current thread successfully entered the <see cref="Semaphore"/>;
        /// otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeout"/> is a negative
        /// number other than -1 milliseconds, which represents an infinite time-out -or- timeout is greater
        /// than <see cref="int.MaxValue"/>.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public bool Wait(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return _internalSemaphore.Wait(timeout, cancellationToken);
        }

        /// <summary>
        /// Blocks the current thread until it can enter the <see cref="Semaphore"/>,
        /// using a 32-bit signed integer to measure the time interval,
        /// while observing a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="millisecondsTimeout">The number of milliseconds to wait, or <see cref="Timeout.Infinite"/>(-1) to
        /// wait indefinitely.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <returns>true if the current thread successfully entered the <see cref="Semaphore"/>; otherwise, false.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is a negative number other than -1,
        /// which represents an infinite time-out.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        public bool Wait(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return _internalSemaphore.Wait(millisecondsTimeout, cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits to enter the <see cref="Semaphore"/>, while observing a
        /// <see cref="CancellationToken"/>.
        /// </summary>
        /// <returns>A task that will complete when the semaphore has been entered.</returns>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> token to observe.
        /// </param>
        /// <exception cref="ObjectDisposedException">
        /// The current instance has already been disposed.
        /// </exception>
        public Task WaitAsync(CancellationToken cancellationToken)
        {
            return _internalSemaphore.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits to enter the <see cref="Semaphore"/>, using a <see
        /// cref="TimeSpan"/> to measure the time interval.
        /// </summary>
        /// <param name="timeout">
        /// A <see cref="TimeSpan"/> that represents the number of milliseconds
        /// to wait, or a <see cref="TimeSpan"/> that represents -1 milliseconds to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">
        /// The <see cref="CancellationToken"/> token to observe.
        /// </param>
        /// <returns>
        /// A task that will complete with a result of true if the current thread successfully entered
        /// the <see cref="Semaphore"/>, otherwise with a result of false.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="timeout"/> is a negative number other than -1 milliseconds, which represents
        /// an infinite time-out -or- timeout is greater than <see cref="int.MaxValue"/>.
        /// </exception>
        public Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
        {
            return _internalSemaphore.WaitAsync(timeout, cancellationToken);
        }

        /// <summary>
        /// Asynchronously waits to enter the <see cref="Semaphore"/>,
        /// using a 32-bit signed integer to measure the time interval,
        /// while observing a <see cref="CancellationToken"/>.
        /// </summary>
        /// <param name="millisecondsTimeout">
        /// The number of milliseconds to wait, or <see cref="Timeout.Infinite"/>(-1) to wait indefinitely.
        /// </param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe.</param>
        /// <returns>
        /// A task that will complete with a result of true if the current thread successfully entered
        /// the <see cref="Semaphore"/>, otherwise with a result of false.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been
        /// disposed.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="millisecondsTimeout"/> is a negative number other than -1,
        /// which represents an infinite time-out.
        /// </exception>
        public Task<bool> WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken)
        {
            return _internalSemaphore.WaitAsync(millisecondsTimeout, cancellationToken);
        }

        /// <summary>
        /// Exits the <see cref="Semaphore"/> once.
        /// </summary>
        /// <returns>The previous count of the <see cref="Semaphore"/>.</returns>
        /// <exception cref="ObjectDisposedException">The current instance has already been
        /// disposed.</exception>
        public int Release()
        {
            return _internalSemaphore.Release();
        }

        /// <summary>
        /// Exits the <see cref="Semaphore"/> a specified number of times.
        /// </summary>
        /// <param name="releaseCount">The number of times to exit the semaphore.</param>
        /// <returns>The previous count of the <see cref="Semaphore"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="releaseCount"/> is less
        /// than 1.</exception>
        /// <exception cref="SemaphoreFullException">The <see cref="Semaphore"/> has
        /// already reached its maximum size.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been
        /// disposed.</exception>
        public int Release(int releaseCount)
        {
            return _internalSemaphore.Release(releaseCount);
        }
    }
}
