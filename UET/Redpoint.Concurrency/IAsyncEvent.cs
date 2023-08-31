namespace Redpoint.Concurrency
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// The public facing interface for asynchronous events. Classes
    /// should expose asynchronous events using this interface instead
    /// of surfacing <see cref="AsyncEvent"/> directly.
    /// </summary>
    public interface IAsyncEvent
    {
        /// <summary>
        /// Add a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        void Add(Func<CancellationToken, Task> handler);

        /// <summary>
        /// Add a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        Task AddAsync(Func<CancellationToken, Task> handler);

        /// <summary>
        /// Remove a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        void Remove(Func<CancellationToken, Task> handler);

        /// <summary>
        /// Remove a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        Task RemoveAsync(Func<CancellationToken, Task> handler);
    }

    /// <summary>
    /// The public facing interface for asynchronous events. Classes
    /// should expose asynchronous events using this interface instead
    /// of surfacing <see cref="AsyncEvent{TArgs}"/> directly.
    /// </summary>
    public interface IAsyncEvent<TArgs>
    {
        /// <summary>
        /// Add a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        void Add(Func<TArgs, CancellationToken, Task> handler);

        /// <summary>
        /// Add a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        Task AddAsync(Func<TArgs, CancellationToken, Task> handler);

        /// <summary>
        /// Remove a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        void Remove(Func<TArgs, CancellationToken, Task> handler);

        /// <summary>
        /// Remove a handler to the asynchronous event.
        /// </summary>
        /// <param name="handler">The handler to add.</param>
        /// <returns>An awaitable task for when the handler has been added.</returns>
        Task RemoveAsync(Func<TArgs, CancellationToken, Task> handler);
    }
}
