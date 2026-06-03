namespace Redpoint.CloudFramework.Counter
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Datastore;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Tracing;
    using Redpoint.Collections;
    using Redpoint.Concurrency;
    using StackExchange.Redis;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Threading.Tasks;

    internal class DefaultGlobalShardedCounter : IGlobalShardedCounter
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IManagedTracer _managedTracer;
        private readonly IDatabase? _redisDatabase;

        // This can only ever be increased; not decreased.
        private const int _numShards = 10;

        private const int _concurrentLoad = 24;

        public DefaultGlobalShardedCounter(
            IGlobalRepository globalRepository,
            IManagedTracer managedTracer,
            IConnectionMultiplexer? connectionMultiplexer = null)
        {
            ArgumentNullException.ThrowIfNull(connectionMultiplexer);

            _globalRepository = globalRepository;
            _managedTracer = managedTracer;
            if (connectionMultiplexer != null)
            {
                _redisDatabase = connectionMultiplexer.GetDatabase();
            }
            else
            {
                _redisDatabase = null;
            }
        }

        private static string GetShardKeyName(ShardedCounterName name, long index)
        {
            return $"{name.name}:{index}";
        }

        private static RedisKey GetShardRedisName(string @namespace, ShardedCounterName name)
        {
            return $"shard:{@namespace}:{name.name}";
        }

        private async IAsyncEnumerable<Key> GetAllKeys(string @namespace, ShardedCounterName name)
        {
            var keyFactory = await _globalRepository.GetKeyFactoryAsync<DefaultShardedCounterModel>(@namespace).ConfigureAwait(false);
            for (var i = 0; i < _numShards; i++)
            {
                yield return keyFactory.CreateKey(GetShardKeyName(name, i));
            }
        }

        private async Task StoreToRedisAsync(string @namespace, ShardedCounterName name, long total)
        {
            if (_redisDatabase != null)
            {
                await _redisDatabase.StringSetAsync(
                    GetShardRedisName(@namespace, name),
                    total,
                    expiry: null,
                    When.NotExists).ConfigureAwait(false);
            }
        }

        public async Task<long> GetAsync(string @namespace, ShardedCounterName name)
        {
            using (_managedTracer.StartSpan("db.counter.get", name.name))
            {
                long total;
                if (_redisDatabase != null)
                {
                    var shardCache = await _redisDatabase.StringGetAsync(GetShardRedisName(@namespace, name)).ConfigureAwait(false);
                    // @note: shardCache.IsInteger is incorrect, because that is for when Redis values are exposed as integers, and not when Redis values are integers internally but exposed as strings to clients.
                    if (shardCache.HasValue && shardCache.TryParse(out total))
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
                    await StoreToRedisAsync(@namespace, name, total);
                }
                return total;
            }
        }

        private class WaitingOn : IDisposable
        {
            public required SemaphoreSlim Semaphore;

            public required ShardedCounterName Name;

            public required Dictionary<Key, long?> Shards;

            public required int RemainingCount;

            public required Future<long> Future;

            public void Dispose()
            {
                ((IDisposable)Semaphore).Dispose();
            }
        }

        public IReadOnlyDictionary<ShardedCounterName, Task<long>> GetManyAsync(string @namespace, IEnumerable<ShardedCounterName> name, CancellationToken cancellationToken)
        {
            var names = name.ToList();

            // Create the dictionary of promises and return the tasks that yield them.
            var futures = names.ToDictionary(k => k, v => new Future<long>());

            // Run the 
            _ = Task.Run(async () =>
            {
                try
                {
                    // First pass, load all values from Redis. We don't start loading from Datastore until we've done all of
                    // this step, because we want to be able to batch load all of the missing counters.
                    HashSet<ShardedCounterName> notInRedisCache;
                    if (_redisDatabase != null)
                    {
                        // Load all of them at once.
                        notInRedisCache = new();
                        var futuresOrdered = futures.ToArray();
                        var redisKeys = new RedisKey[futuresOrdered.Length];
                        for (int i = 0; i < futuresOrdered.Length; i++)
                        {
                            redisKeys[i] = GetShardRedisName(@namespace, futuresOrdered[i].Key);
                        }
                        var shardCaches = await _redisDatabase.StringGetAsync(
                            futuresOrdered.Select(kv => GetShardRedisName(@namespace, kv.Key)).ToArray());
                        for (int i = 0; i < futuresOrdered.Length; i++)
                        {
                            var shardCache = shardCaches[i];
                            var future = futuresOrdered[i];
                            if (shardCache.HasValue && shardCache.TryParse(out long total))
                            {
                                future.Value.SetValue(total);
                            }
                            else
                            {
                                notInRedisCache.Add(future.Key);
                            }
                        }
                    }
                    else
                    {
                        notInRedisCache = futures.Keys.ToHashSet();
                    }

                    // Anything in 'notInRedisCache' now needs to be loaded from Datastore.
                    var keyFactory = await _globalRepository.GetKeyFactoryAsync<DefaultShardedCounterModel>(@namespace).ConfigureAwait(false);

                    var waitingOn = new List<WaitingOn>();
                    try
                    {
                        var keysToWaitingOn = new ConcurrentDictionary<Key, WaitingOn>();
                        foreach (var name in notInRedisCache)
                        {
                            var shards = Enumerable.Range(0, _numShards)
                                .ToDictionary(
                                    i => keyFactory.CreateKey(GetShardKeyName(name, i)),
                                    i => (long?)null);
                            waitingOn.Add(new WaitingOn
                            {
                                Semaphore = new SemaphoreSlim(1),
                                Name = name,
                                Shards = shards,
                                RemainingCount = shards.Count,
                                Future = futures[name],
                            });
                        }
                        foreach (var waiting in waitingOn)
                        {
                            foreach (var kv in waiting.Shards)
                            {
                                keysToWaitingOn.TryAdd(kv.Key, waiting);
                            }
                        }
                        await foreach (var _ in _globalRepository
                            .LoadAsync<DefaultShardedCounterModel>(
                                @namespace,
                                keysToWaitingOn.Keys.ToAsyncEnumerable(),
                                cancellationToken: cancellationToken)
                            .SelectFast(
                                _concurrentLoad,
                                async kv =>
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    if (keysToWaitingOn.TryGetValue(kv.Key, out var waiting))
                                    {
                                        await waiting.Semaphore.WaitAsync().ConfigureAwait(false);
                                        try
                                        {
                                            // Fill in the shard value.
                                            if (waiting.Shards.TryGetValue(kv.Key, out var previousValue) &&
                                                previousValue == null)
                                            {
                                                waiting.Shards[kv.Key] = kv.Value == null ? 0L : kv.Value.value;
                                                waiting.RemainingCount--;
                                            }

                                            // Check if this result is ready.
                                            if (!waiting.Future.IsCompleted && waiting.RemainingCount == 0)
                                            {
                                                long total = 0;
                                                foreach (var value in waiting.Shards.Values)
                                                {
                                                    total += value ?? 0L;
                                                }
                                                waiting.Future.SetValue(total);

                                                // Update in Redis.
                                                if (_redisDatabase != null)
                                                {
                                                    await StoreToRedisAsync(@namespace, waiting.Name, total);
                                                    cancellationToken.ThrowIfCancellationRequested();
                                                }
                                            }
                                        }
                                        finally
                                        {
                                            waiting.Semaphore.Release();
                                        }
                                    }
                                    else
                                    {
                                        throw new InvalidOperationException("Received counter model with unexpected key!");
                                    }
                                    return true;
                                }).ConfigureAwait(false))
                        {
                            // Do nothing with the result; SelectFast sends it where it needs to be.
                        }

                        // Make sure enumerating the SelectFast actually gave us all expected values.
                        foreach (var kv in waitingOn)
                        {
                            if (kv.RemainingCount > 0 ||
                                kv.Shards.Values.Any(x => x == null) ||
                                !kv.Future.IsCompleted)
                            {
                                throw new InvalidOperationException("SelectFast did not process all expected values.");
                            }
                        }
                    }
                    finally
                    {
                        foreach (var waiting in waitingOn)
                        {
                            waiting.Dispose();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // If we have an exception (including cancellation), update any futures that
                    // don't have values set yet. This ensures that if GetManyAsync is cancelled,
                    // the cancellation propagates to anything awaiting the returned tasks.
                    foreach (var future in futures)
                    {
                        if (!future.Value.IsCompleted)
                        {
                            future.Value.SetException(ex);
                        }
                    }
                }
            }, cancellationToken);

            // Return dictionary where individual values can be awaited.
            return futures.ToDictionary(
                kv => kv.Key,
                kv =>
                {
                    async Task<long> waiter()
                    {
                        return await kv.Value;
                    }
                    return waiter();
                });
        }

        public async Task AdjustAsync(string @namespace, ShardedCounterName name, long modifier)
        {
            using (_managedTracer.StartSpan("db.counter.adjust", name.name))
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
        }

        public async Task<ShardedCounterPostCommit> AdjustAsync(string @namespace, ShardedCounterName name, long modifier, IModelTransaction transaction)
        {
            using (_managedTracer.StartSpan("db.counter.adjust_in_transaction", name.name))
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
                        name = name.name,
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
}
