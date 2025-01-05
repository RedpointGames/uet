namespace Redpoint.CloudFramework.Locking
{
    using Google.Cloud.Datastore.V1;
    using System;
    using System.Threading.Tasks;

    public interface IGlobalLockService
    {
        /// <summary>
        /// Acquires a lock in the given namespace on the given object's key. Returns the lock handle
        /// which you should then call and await <see cref="IAsyncDisposable.DisposeAsync"/>, which will release
        /// the lock globally. By default locks have an expiry of 5 minutes, and a background task is
        /// automatically spawned in the current process to extend the expiration every minute. This ensures
        /// that if this process crashes, another process will be able to obtain the lock within 5
        /// minutes of the current process going away.
        /// </summary>
        /// <param name="namespace">The datastore namespace to store the lock object in.</param>
        /// <param name="objectToLock">The object key to lock on.</param>
        /// <returns>The lock handle which you should then call and await <see cref="IAsyncDisposable.DisposeAsync"/>.</returns>
        Task<ILockHandle> Acquire(string @namespace, Key objectToLock);

        /// <summary>
        /// Acquires a lock in the given namespace using <see cref="Acquire(string, Key)"/>, and then calls
        /// the given lambda asynchronously. When the lambda completes for any reason, the lock is released.
        /// </summary>
        /// <param name="namespace">The datastore namespace to store the lock object in.</param>
        /// <param name="objectToLock">The object key to lock on.</param>
        /// <param name="block">The lambda to execute while the lock is held.</param>
        /// <returns>The task that you should await on.</returns>
        Task AcquireAndUse(string @namespace, Key objectToLock, Func<Task> block);

        /// <summary>
        /// Acquires a lock in the given namespace using <see cref="Acquire(string, Key)"/>, and then calls
        /// the given lambda asynchronously. When the lambda completes for any reason, the lock is released.
        /// </summary>
        /// <param name="namespace">The datastore namespace to store the lock object in.</param>
        /// <param name="objectToLock">The object key to lock on.</param>
        /// <param name="block">The lambda to execute while the lock is held.</param>
        /// <returns>The task with return value that you should await on.</returns>
        Task<T> AcquireAndUse<T>(string @namespace, Key objectToLock, Func<Task<T>> block);
    }
}
