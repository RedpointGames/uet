﻿namespace Redpoint.CloudFramework.Counter
{
    using Redpoint.CloudFramework.Repository.Transaction;
    using System.Threading.Tasks;

    public interface IShardedCounter
    {
        /// <summary>
        /// Returns the value of a sharded counter.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <returns>The value of the sharded counter.</returns>
        Task<long> GetAsync(string name);

        /// <summary>
        /// Adjust the value of a sharded counter.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <returns>The task to await on.</returns>
        Task AdjustAsync(string name, long modifier);

        /// <summary>
        /// Adjust the value of a sharded counter inside an existing transaction. You *must* await this
        /// function and call the callback it returns after you commit the provided transaction.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <param name="existingTransaction">The existing transaction to update the counter in.</param>
        /// <returns>The task to await on.</returns>
        Task<ShardedCounterPostCommit> AdjustAsync(string name, long modifier, IModelTransaction existingTransaction);
    }
}