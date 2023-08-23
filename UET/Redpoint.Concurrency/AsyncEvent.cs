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
        private readonly SemaphoreSlim _handlersLock;

        /// <summary>
        /// Construct a new asynchronous event.
        /// </summary>
        public AsyncEvent()
        {
            _handlers = new List<Func<CancellationToken, Task>>();
            _handlersLock = new SemaphoreSlim(1);
        }

        /// <inheritdoc />
        public async Task AddAsync(Func<CancellationToken, Task> handler)
        {
            await _handlersLock.WaitAsync();
            try
            {
                _handlers.Add(handler);
            }
            finally
            {
                _handlersLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task RemoveAsync(Func<CancellationToken, Task> handler)
        {
            await _handlersLock.WaitAsync();
            try
            {
                _handlers.Remove(handler);
            }
            finally
            {
                _handlersLock.Release();
            }
        }

        /// <summary>
        /// Broadcast the event.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task for when all handlers have completed.</returns>
        public async Task BroadcastAsync(CancellationToken cancellationToken)
        {
            await Parallel.ForEachAsync(
                _handlers,
                cancellationToken,
                async (handler, ct) =>
                {
                    await handler(ct);
                });
        }
    }

    /// <summary>
    /// Represents an asynchronous, broadcastable event where the broadcast
    /// and handlers take no arguments.
    /// </summary>
    public class AsyncEvent<TArgs> : IAsyncEvent<TArgs>
    {
        private readonly List<Func<TArgs, CancellationToken, Task>> _handlers;
        private readonly SemaphoreSlim _handlersLock;

        /// <summary>
        /// Construct a new asynchronous event.
        /// </summary>
        public AsyncEvent()
        {
            _handlers = new List<Func<TArgs, CancellationToken, Task>>();
            _handlersLock = new SemaphoreSlim(1);
        }

        /// <inheritdoc />
        public async Task AddAsync(Func<TArgs, CancellationToken, Task> handler)
        {
            await _handlersLock.WaitAsync();
            try
            {
                _handlers.Add(handler);
            }
            finally
            {
                _handlersLock.Release();
            }
        }

        /// <inheritdoc />
        public async Task RemoveAsync(Func<TArgs, CancellationToken, Task> handler)
        {
            await _handlersLock.WaitAsync();
            try
            {
                _handlers.Remove(handler);
            }
            finally
            {
                _handlersLock.Release();
            }
        }

        /// <summary>
        /// Broadcast the event.
        /// </summary>
        /// <param name="args">The arguments for the event.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An awaitable task for when all handlers have completed.</returns>
        public async Task BroadcastAsync(TArgs args, CancellationToken cancellationToken)
        {
            await Parallel.ForEachAsync(
                _handlers,
                cancellationToken,
                async (handler, ct) =>
                {
                    await handler(args, ct);
                });
        }
    }
}
