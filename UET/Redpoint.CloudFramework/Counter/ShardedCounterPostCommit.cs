namespace Redpoint.CloudFramework.Counter
{
    using System.Threading.Tasks;

    /// <summary>
    /// The callback returned from <see cref="IShardedCounter.AdjustAsync(ShardedCounterName, long, Repository.Transaction.IModelTransaction)"/> and <see cref="IGlobalShardedCounter.AdjustAsync(string, ShardedCounterName, long, Repository.Transaction.IModelTransaction)"/> which MUST be called after the transaction has been committed.
    /// </summary>
    /// <returns></returns>
    public delegate Task ShardedCounterPostCommit();
}
