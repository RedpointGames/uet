namespace Redpoint.CloudFramework.Counter
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    [Obsolete("Use IShardedCounter instead.")]
    public interface IShardedCounterService
    {
        /// <summary>
        /// Returns the value of a sharded counter.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <returns>The value of the sharded counter.</returns>
        Task<long> Get(string name);

        /// <summary>
        /// Returns the value of a sharded counter stored in a custom model.
        /// </summary>
        /// <typeparam name="T">The model that is used to store sharded counter data.</typeparam>
        /// <param name="name">The name of the sharded counter.</param>
        /// <returns>The value of the sharded counter.</returns>
        Task<long> GetCustom<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(string name) where T : Model, IShardedCounterModel, new();

        /// <summary>
        /// Adjust the value of a sharded counter.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <returns>The task to await on.</returns>
        Task Adjust(string name, long modifier);

        /// <summary>
        /// Adjust the value of a sharded counter stored in a custom model.
        /// </summary>
        /// <typeparam name="T">The model that is used to store sharded counter data.</typeparam>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <returns>The task to await on.</returns>
        Task AdjustCustom<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(string name, long modifier) where T : Model, IShardedCounterModel, new();

        /// <summary>
        /// Adjust the value of a sharded counter inside an existing transaction. You *must* await this
        /// function and call the callback it returns after you commit the provided transaction.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <param name="existingTransaction">The existing transaction to update the counter in.</param>
        /// <returns>The task to await on.</returns>
        Task<Func<Task>> Adjust(string name, long modifier, IModelTransaction existingTransaction);

        /// <summary>
        /// Adjust the value of a sharded counter stored in a custom model, inside an existing transaction.
        /// You *must* await this function and call the callback it returns after you commit the provided transaction.
        /// </summary>
        /// <param name="name">The name of the sharded counter.</param>
        /// <param name="modifier">The amount to modify the sharded counter by.</param>
        /// <param name="existingTransaction">The existing transaction to update the counter in.</param>
        /// <returns>The task to await on.</returns>
        Task<Func<Task>> AdjustCustom<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(string name, long modifier, IModelTransaction existingTransaction) where T : Model, IShardedCounterModel, new();
    }
}
