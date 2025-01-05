namespace Redpoint.CloudFramework.Counter
{
    using Redpoint.CloudFramework.Repository.Transaction;
    using System;
    using System.Threading.Tasks;

    internal class DefaultShardedCounter : IShardedCounter
    {
        private readonly IGlobalShardedCounter _globalShardedCounter;
        private readonly ICurrentTenantService _currentTenant;

        public DefaultShardedCounter(
            IGlobalShardedCounter globalShardedCounter,
            ICurrentTenantService currentTenant)
        {
            _globalShardedCounter = globalShardedCounter;
            _currentTenant = currentTenant;
        }

        private async Task<string> GetDatastoreNamespace()
        {
            var currentTenant = await _currentTenant.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("IShardedCounter can not be used without a tenant.");
            }
            return currentTenant.DatastoreNamespace;
        }

        public async Task<long> GetAsync(string name)
        {
            return await _globalShardedCounter.GetAsync(
                await GetDatastoreNamespace().ConfigureAwait(false),
                name).ConfigureAwait(false);
        }

        public async Task AdjustAsync(string name, long modifier)
        {
            await _globalShardedCounter.AdjustAsync(
                await GetDatastoreNamespace().ConfigureAwait(false),
                name,
                modifier).ConfigureAwait(false);
        }

        public async Task<ShardedCounterPostCommit> AdjustAsync(string name, long modifier, IModelTransaction existingTransaction)
        {
            return await _globalShardedCounter.AdjustAsync(
                await GetDatastoreNamespace().ConfigureAwait(false),
                name,
                modifier,
                existingTransaction).ConfigureAwait(false);
        }
    }
}
