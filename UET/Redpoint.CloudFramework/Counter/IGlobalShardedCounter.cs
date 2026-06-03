namespace Redpoint.CloudFramework.Counter
{
    using Redpoint.CloudFramework.Repository.Transaction;
    using System.Threading.Tasks;

    public interface IGlobalShardedCounter
    {
        /// <summary>
        /// Returns the value of a sharded counter.
        /// </summary>
        /// <param name="namespace">The Datastore namespace to store the counter in.</param>
        /// <param name="name">The name of the sharded counter.</param>
        /// <returns>The value of the sharded counter.</returns>
        Task<long> GetAsync(string @namespace, ShardedCounterName name);

        /// <summary>
        /// Returns the values of multiple sharded counters, with tasks that yield values as soon as the value for a particular sharded counter is available, while also batching Datastore loads for efficiency.
        /// </summary>
        /// <param name="namespace">The Datastore namespace to store the counter in.</param>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="cancellationToken">Cancels any remaining tasks that haven't returned yet.</param>
        /// <returns></returns>
        IReadOnlyDictionary<ShardedCounterName, Task<long>> GetManyAsync(string @namespace, IEnumerable<ShardedCounterName> name, CancellationToken cancellationToken);

        /// <summary>
        /// Adjust the value of a sharded counter.
        /// </summary>
        /// <param name="namespace">The Datastore namespace to store the counter in.</param>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <returns>The task to await on.</returns>
        Task AdjustAsync(string @namespace, ShardedCounterName name, long modifier);

        /// <summary>
        /// Adjust the value of a sharded counter inside an existing transaction. You *must* await this
        /// function and call the callback it returns after you commit the provided transaction.
        /// </summary>
        /// <param name="namespace">The Datastore namespace to store the counter in.</param>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <param name="existingTransaction">The existing transaction to update the counter in.</param>
        /// <returns>The task to await on.</returns>
        Task<ShardedCounterPostCommit> AdjustAsync(string @namespace, ShardedCounterName name, long modifier, IModelTransaction existingTransaction);
    }
}
