namespace Redpoint.CloudFramework.Models
{
    [Obsolete("This interface is no longer used by IShardedCounter and IGlobalShardedCounter.")]
    public interface IShardedCounterModel
    {
        /// <summary>
        /// If specified, this Datastore field on the entity will have it's value set to "shard".
        /// </summary>
        /// <returns>The Datastore field name, or null for no field.</returns>
        string? GetTypeFieldName();

        /// <summary>
        /// The name of the field to actually store the count in.  The <see cref="Counter.IShardedCounterService"/> bypasses
        /// the regular ORM datastore layer for performance, so it needs to explicitly know the Datastore field name here.
        /// </summary>
        /// <returns>The Datastore field name.</returns>
        string GetCountFieldName();

        /// <summary>
        /// Converts the sharded counter name and shard index into the name for the Datastore key.
        /// </summary>
        /// <param name="name">The sharded counter name.</param>
        /// <param name="index">The index of the shard in the counter.</param>
        /// <returns>The formatted name.</returns>
        string FormatShardName(string name, int index);
    }
}
