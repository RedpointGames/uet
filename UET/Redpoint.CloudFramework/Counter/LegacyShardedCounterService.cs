namespace Redpoint.CloudFramework.Counter
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Transaction;
    using StackExchange.Redis;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

#pragma warning disable CS0612 // Type or member is obsolete
#pragma warning disable CS0618 // Type or member is obsolete
    internal class LegacyShardedCounterService : IShardedCounterService
    {
        private readonly IRepository _repository;
        private readonly IDatabase _redisDatabase;
        private readonly Random _random;

        // This can only ever be increased; not decreased.
        public const int NumShards = 60;

        private const bool _enableRedis = true;

        public LegacyShardedCounterService(
            IRepository repository,
            IConnectionMultiplexer connectionMultiplexer)
        {
            ArgumentNullException.ThrowIfNull(connectionMultiplexer);

            _repository = repository;
            _redisDatabase = connectionMultiplexer.GetDatabase();
            _random = new Random();
        }

        private async IAsyncEnumerable<Key> GetAllKeys<T>(string name) where T : Model, IShardedCounterModel, new()
        {
            var t = new T();

            var keyFactory = await _repository.GetKeyFactoryAsync<T>().ConfigureAwait(false);
            for (var i = 0; i < NumShards; i++)
            {
                yield return keyFactory.CreateKey(t.FormatShardName(name, i));
            }
        }

        public async Task AdjustCustom<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(string name, long modifier) where T : Model, IShardedCounterModel, new()
        {
            var t = new T();

            var counterProperty = typeof(T).GetProperty(t.GetCountFieldName(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (counterProperty == null)
            {
                throw new InvalidOperationException($"The count field name specified by GetCountFieldName '{t.GetCountFieldName()}' does not exist on the class.");
            }

            var typeName = t.GetTypeFieldName();
            var typeProperty = typeName == null ? null : typeof(T).GetProperty(typeName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

#pragma warning disable CA5394 // Do not use insecure randomness
            var idx = _random.Next(0, NumShards);
#pragma warning restore CA5394 // Do not use insecure randomness
            var keyFactory = await _repository.GetKeyFactoryAsync<T>().ConfigureAwait(false);
            var key = keyFactory.CreateKey(t.FormatShardName(name, idx));
            var transaction = await _repository.BeginTransactionAsync().ConfigureAwait(false);
            var rollback = true;
            try
            {
                var create = false;
                var counter = await _repository.LoadAsync<T>(key, transaction).ConfigureAwait(false);
                if (counter == null)
                {
                    counter = new T
                    {
                        Key = key,
                    };
                    counterProperty.SetValue(counter, modifier);
                    if (typeProperty != null)
                    {
                        typeProperty.SetValue(counter, "shard");
                    }
                    create = true;
                }
                else
                {
                    counterProperty.SetValue(counter, ((long?)counterProperty.GetValue(counter) ?? 0) + modifier);
                }
                if (create)
                {
                    await _repository.CreateAsync(counter, transaction).ConfigureAwait(false);
                }
                else
                {
                    await _repository.UpdateAsync(counter, transaction).ConfigureAwait(false);
                }
                await _repository.CommitAsync(transaction).ConfigureAwait(false);
                if (_enableRedis)
                {
                    await _redisDatabase.StringIncrementAsync("shard-" + name, modifier, CommandFlags.FireAndForget).ConfigureAwait(false);
                }
                rollback = false;
            }
            finally
            {
                if (rollback)
                {
                    await transaction.Transaction.RollbackAsync().ConfigureAwait(false);
                }
            }
        }


        public async Task<Func<Task>> AdjustCustom<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(string name, long modifier, IModelTransaction existingTransaction) where T : Model, IShardedCounterModel, new()
        {
            var t = new T();

            var counterProperty = typeof(T).GetProperty(t.GetCountFieldName(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (counterProperty == null)
            {
                throw new InvalidOperationException($"The count field name specified by GetCountFieldName '{t.GetCountFieldName()}' does not exist on the class.");
            }

            var typeName = t.GetTypeFieldName();
            var typeProperty = typeName == null ? null : typeof(T).GetProperty(typeName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

#pragma warning disable CA5394 // Do not use insecure randomness
            var idx = _random.Next(0, NumShards);
#pragma warning restore CA5394 // Do not use insecure randomness
            var keyFactory = await _repository.GetKeyFactoryAsync<T>().ConfigureAwait(false);
            var key = keyFactory.CreateKey(t.FormatShardName(name, idx));

            var create = false;
            var counter = await _repository.LoadAsync<T>(key, existingTransaction).ConfigureAwait(false);
            if (counter == null)
            {
                counter = new T
                {
                    Key = key,
                };
                counterProperty.SetValue(counter, modifier);
                if (typeProperty != null)
                {
                    typeProperty.SetValue(counter, "shard");
                }
                create = true;
            }
            else
            {
                counterProperty.SetValue(counter, ((long?)counterProperty.GetValue(counter) ?? 0) + modifier);
            }
            if (create)
            {
                await _repository.CreateAsync(counter, existingTransaction).ConfigureAwait(false);
            }
            else
            {
                await _repository.UpdateAsync(counter, existingTransaction).ConfigureAwait(false);
            }
            return async () =>
            {
                if (_enableRedis)
                {
                    await _redisDatabase.StringIncrementAsync("shard-" + name, modifier, CommandFlags.FireAndForget).ConfigureAwait(false);
                }
            };
        }

        public async Task<long> GetCustom<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] T>(string name) where T : Model, IShardedCounterModel, new()
        {
            var t = new T();

            var counterProperty = typeof(T).GetProperty(t.GetCountFieldName(), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (counterProperty == null)
            {
                throw new InvalidOperationException($"The count field name specified by GetCountFieldName '{t.GetCountFieldName()}' does not exist on the class.");
            }

            long total;
            if (_enableRedis)
            {
                var shardCache = await _redisDatabase.StringGetAsync("shard-" + name).ConfigureAwait(false);
                if (!(!shardCache.HasValue || !shardCache.IsInteger || !shardCache.TryParse(out total)))
                {
                    return total;
                }
            }

            total =
                await _repository.LoadAsync<T>(GetAllKeys<T>(name))
                .Where(x => x.Value != null)
                .Select(x => (long?)counterProperty.GetValue(x.Value) ?? 0)
                .SumAsync().ConfigureAwait(false);
            if (_enableRedis)
            {
                await _redisDatabase.StringSetAsync(
                    "shard-" + name,
                    total,
                    TimeSpan.FromSeconds(60),
                    When.NotExists).ConfigureAwait(false);
            }
            return total;
        }

        public Task<long> Get(string name)
        {
            return GetCustom<LegacyShardedCounterModel>(name);
        }

        public Task Adjust(string name, long modifier)
        {
            return AdjustCustom<LegacyShardedCounterModel>(name, modifier);
        }

        public Task<Func<Task>> Adjust(string name, long modifier, IModelTransaction existingTransaction)
        {
            return AdjustCustom<LegacyShardedCounterModel>(name, modifier, existingTransaction);
        }
    }
#pragma warning restore CS0612 // Type or member is obsolete
#pragma warning restore CS0618 // Type or member is obsolete
}
