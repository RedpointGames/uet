namespace Redpoint.Concurrency
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents an asynchronous, broadcastable event where the broadcast
    /// and handlers take no arguments.
    /// </summary>
    public class AsyncEvent : IAsyncEvent
    {
        private readonly List<Func<CancellationToken, Task>> _handlers;
        private readonly Mutex _handlersLock;

        /// <summary>
        /// Construct a new asynchronous event.
        /// </summary>
        public AsyncEvent()
        {
            _handlers = new List<Func<CancellationToken, Task>>();
            _handlersLock = new Mutex();
        }

        /// <inheritdoc />
        public void Add(Func<CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = _handlersLock.Wait(CancellationToken.None);
            _handlers.Add(handler);
        }

        /// <inheritdoc />
        public async Task AddAsync(Func<CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = await _handlersLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _handlers.Add(handler);
        }

        /// <inheritdoc />
        public void Remove(Func<CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = _handlersLock.Wait(CancellationToken.None);
            _handlers.Remove(handler);
        }

        /// <inheritdoc />
        public async Task RemoveAsync(Func<CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = await _handlersLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _handlers.Remove(handler);
        }

        /// <summary>
        /// Broadcast the event.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task for when all handlers have completed.</returns>
        public async Task BroadcastAsync(CancellationToken cancellationToken)
        {
            Func<CancellationToken, Task>[] handlers;
            using (await _handlersLock.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                handlers = _handlers.ToArray();
            }
            if (handlers != null && handlers.Length > 0)
            {
                await Parallel.ForEachAsync(
                handlers,
                cancellationToken,
                async (handler, ct) =>
                {
                    if (handler == null)
                    {
                        throw new InvalidOperationException($"'handler' is null in BroadcastAsync, which should not be possible.");
                    }
                    await handler(ct).ConfigureAwait(false);
                }).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Represents an asynchronous, broadcastable event where the broadcast
    /// and handlers take no arguments.
    /// </summary>
    public class AsyncEvent<TArgs> : IAsyncEvent<TArgs>
    {
        private readonly List<Func<TArgs, CancellationToken, Task>> _handlers;
        private readonly Mutex _handlersLock;

        /// <summary>
        /// Construct a new asynchronous event.
        /// </summary>
        public AsyncEvent()
        {
            _handlers = new List<Func<TArgs, CancellationToken, Task>>();
            _handlersLock = new Mutex();
        }

        /// <inheritdoc />
        public void Add(Func<TArgs, CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = _handlersLock.Wait(CancellationToken.None);
            _handlers.Add(handler);
        }

        /// <inheritdoc />
        public async Task AddAsync(Func<TArgs, CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = await _handlersLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _handlers.Add(handler);
        }

        /// <inheritdoc />
        public void Remove(Func<TArgs, CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = _handlersLock.Wait(CancellationToken.None);
            _handlers.Remove(handler);
        }

        /// <inheritdoc />
        public async Task RemoveAsync(Func<TArgs, CancellationToken, Task> handler)
        {
            ArgumentNullException.ThrowIfNull(handler);
            using var _ = await _handlersLock.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            _handlers.Remove(handler);
        }

        /// <summary>
        /// Broadcast the event.
        /// </summary>
        /// <param name="args">The arguments for the event.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task for when all handlers have completed.</returns>
        public async Task BroadcastAsync(TArgs args, CancellationToken cancellationToken)
        {
            Func<TArgs, CancellationToken, Task>[] handlers;
            using (await _handlersLock.WaitAsync(cancellationToken).ConfigureAwait(false))
            {
                handlers = _handlers.ToArray();
            }
            if (handlers != null && handlers.Length > 0)
            {
                await Parallel.ForEachAsync(
                    handlers,
                    cancellationToken,
                    async (handler, ct) =>
                    {
                        if (handler == null)
                        {
                            throw new InvalidOperationException($"'handler' is null in BroadcastAsync, which should not be possible.");
                        }
                        await handler(args, ct).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Returns true if there are any handlers bound to this event.
        /// </summary>
        public bool HasAnyBindings
        {
            get
            {
                using var _ = _handlersLock.Wait(CancellationToken.None);
                return _handlers.Count > 0;
            }
        }
    }
}
