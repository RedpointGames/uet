namespace Redpoint.CloudFramework.Counter
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Transaction;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    internal class DefaultGlobalShardedCounter : IGlobalShardedCounter
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IDatabase? _redisDatabase;

        // This can only ever be increased; not decreased.
        private const int _numShards = 10;

        public DefaultGlobalShardedCounter(
            IGlobalRepository globalRepository,
            IConnectionMultiplexer? connectionMultiplexer = null)
        {
            ArgumentNullException.ThrowIfNull(connectionMultiplexer);

            _globalRepository = globalRepository;
            if (connectionMultiplexer != null)
            {
                _redisDatabase = connectionMultiplexer.GetDatabase();
            }
            else
            {
                _redisDatabase = null;
            }
        }

        private static string GetShardKeyName(string name, long index)
        {
            return $"{name}:{index}";
        }

        private static string GetShardRedisName(string @namespace, string name)
        {
            return $"shard:{@namespace}:{name}";
        }

        private async IAsyncEnumerable<Key> GetAllKeys(string @namespace, string name)
        {
            var keyFactory = await _globalRepository.GetKeyFactoryAsync<DefaultShardedCounterModel>(@namespace).ConfigureAwait(false);
            for (var i = 0; i < _numShards; i++)
            {
                yield return keyFactory.CreateKey(GetShardKeyName(name, i));
            }
        }

        public async Task<long> GetAsync(string @namespace, string name)
        {
            long total;
            if (_redisDatabase != null)
            {
                var shardCache = await _redisDatabase.StringGetAsync(GetShardRedisName(@namespace, name)).ConfigureAwait(false);
                if (!(!shardCache.HasValue || !shardCache.IsInteger || !shardCache.TryParse(out total)))
                {
                    return total;
                }
            }

            total =
                await _globalRepository.LoadAsync<DefaultShardedCounterModel>(
                    @namespace,
                    GetAllKeys(@namespace, name))
                .Where(x => x.Value != null)
                .Select(x => x.Value!.value)
                .SumAsync().ConfigureAwait(false);
            if (_redisDatabase != null)
            {
                await _redisDatabase.StringSetAsync(
                    GetShardRedisName(@namespace, name),
                    total,
                    TimeSpan.FromSeconds(60),
                    When.NotExists).ConfigureAwait(false);
            }
            return total;
        }

        public async Task AdjustAsync(string @namespace, string name, long modifier)
        {
            var transaction = await _globalRepository.BeginTransactionAsync(@namespace).ConfigureAwait(false);
            try
            {
                var afterCommit = await AdjustAsync(@namespace, name, modifier, transaction).ConfigureAwait(false);
                await _globalRepository.CommitAsync(@namespace, transaction).ConfigureAwait(false);
                await afterCommit().ConfigureAwait(false);
            }
            finally
            {
                if (!transaction.HasCommitted)
                {
                    await _globalRepository.RollbackAsync(@namespace, transaction).ConfigureAwait(false);
                }
            }
        }

        public async Task<ShardedCounterPostCommit> AdjustAsync(string @namespace, string name, long modifier, IModelTransaction transaction)
        {
            var index = RandomNumberGenerator.GetInt32(_numShards);
            var keyFactory = await _globalRepository.GetKeyFactoryAsync<DefaultShardedCounterModel>(@namespace).ConfigureAwait(false);
            var key = keyFactory.CreateKey(GetShardKeyName(name, index));

            var create = false;
            var counter = await _globalRepository.LoadAsync<DefaultShardedCounterModel>(@namespace, key, transaction).ConfigureAwait(false);
            if (counter == null)
            {
                counter = new DefaultShardedCounterModel
                {
                    Key = key,
                    name = name,
                    index = index,
                    value = modifier,
                };
                create = true;
            }
            else
            {
                counter.value += modifier;
            }
            if (create)
            {
                await _globalRepository.CreateAsync(@namespace, counter, transaction).ConfigureAwait(false);
            }
            else
            {
                await _globalRepository.UpdateAsync(@namespace, counter, transaction).ConfigureAwait(false);
            }
            return async () =>
            {
                if (_redisDatabase != null)
                {
                    await _redisDatabase.StringIncrementAsync(GetShardRedisName(@namespace, name), modifier, CommandFlags.FireAndForget).ConfigureAwait(false);
                }
            };
        }
    }
}
