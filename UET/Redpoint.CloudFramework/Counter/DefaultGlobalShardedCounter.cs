namespace Redpoint.CloudFramework.Counter
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Tracing;
    using Redpoint.Collections;
    using Redpoint.Concurrency;
    using StackExchange.Redis;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
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

        // key 1 = GetShardedCounterForRedis
        // key 2 = GetShardedCounterFetchStoreTokenForRedis
        // (keys repeat)
        // argv 1 = randomly generated token
        internal const string _loadExistingOrFlagFetchStoreOperation = @"
-- Try to load all of the applicable values, or set fetch-store token for those that don't exist.
local results = {}
for i = 1, (#KEYS / 2) do
    local existing = tonumber(redis.call('GET', KEYS[((i - 1) * 2) + 1]))
    if existing == nil then
        redis.call('SET', KEYS[((i - 1) * 2) + 2], ARGV[1])
        results[i] = ""m""
    else
        results[i] = existing
    end
end
return results
";

        // key 1 = GetShardedCounterForRedis
        // key 2 = GetShardedCounterFetchStoreTokenForRedis
        // (keys repeat)
        // argv 1 = randomly generated token
        // argv 2 = value fetched from Datastore for key 1
        // argv 3 = value fetched from Datastore for key 2
        // (argv repeats)
        internal const string _storeFetchedValueIfFetchStoreOperationUnbroken = @"
-- Store all values that haven't been invalidated by token removal
for i = 1, (#KEYS / 2) do
    local token = redis.call('GET', KEYS[((i - 1) * 2) + 2])
    if token == ARGV[1] then
        redis.call('SET', KEYS[((i - 1) * 2) + 1], ARGV[1 + i])
        redis.call('DEL', KEYS[((i - 1) * 2) + 2])
    end
end
";

        // key 1 = GetShardedCounterForRedis
        // key 2 = GetShardedCounterFetchStoreTokenForRedis
        // argv 1 = incremented by value
        internal const string _adjustOrBreakFetchStoreOperation = @"
-- Check if we have an existing value
local value = tonumber(redis.call('GET', KEYS[1]))
if value == nil then
    -- We do not have a value in Redis, but a GetAsync operation might be about to put a stale value
    -- into it. Prevent that from happening by breaking any fetch-store operation.
    redis.call('DEL', KEYS[2])
else
    -- We have an existing value, perform relative adjustment
    redis.call('INCRBY', KEYS[1], ARGV[1])
end
";

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

        private static string GetShardedCounterIndexForFirestore(ShardedCounterName name, long index)
        {
            return $"{name.name}:{index}";
        }

        private static RedisKey GetShardedCounterForRedis(string @namespace, ShardedCounterName name)
        {
            return $"shard/{@namespace}/{name.name}";
        }

        private static RedisKey GetShardedCounterFetchStoreTokenForRedis(string @namespace, ShardedCounterName name)
        {
            return $"shard-token/{@namespace}/{name.name}";
        }

        private async IAsyncEnumerable<Key> GetAllKeys(string @namespace, ShardedCounterName name)
        {
            var keyFactory = await _globalRepository.GetKeyFactoryAsync<DefaultShardedCounterModel>(@namespace).ConfigureAwait(false);
            for (var i = 0; i < _numShards; i++)
            {
                yield return keyFactory.CreateKey(GetShardedCounterIndexForFirestore(name, i));
            }
        }

        internal static long?[] RedisResultToArrayOfNumbers(RedisResult result)
        {
            var converted = new long?[result.Length];
            for (int i = 0; i < result.Length; i++)
            {
                if (result[i] != null)
                {
                    string? resultAsString = (string?)result[i];
                    if (resultAsString != null &&
                        resultAsString != "m" &&
                        long.TryParse(resultAsString, out long value))
                    {
                        converted[i] = value;
                    }
                    else
                    {
                        converted[i] = null;
                    }
                }
                else
                {
                    converted[i] = null;
                }
            }
            return converted;
        }

        public async Task<long> GetAsync(string @namespace, ShardedCounterName name)
        {
            using (_managedTracer.StartSpan("db.counter.get", name.name))
            {
                long total;
                string? fetchStoreToken = null;
                if (_redisDatabase != null)
                {
                    fetchStoreToken = Guid.NewGuid().ToString();
                    var cachedValues = RedisResultToArrayOfNumbers(await _redisDatabase.ScriptEvaluateAsync(
                        _loadExistingOrFlagFetchStoreOperation,
                        [
                            GetShardedCounterForRedis(@namespace, name),
                            GetShardedCounterFetchStoreTokenForRedis(@namespace, name),
                        ],
                        [
                            fetchStoreToken,
                        ]).ConfigureAwait(false));
                    if (cachedValues.Length > 0 && cachedValues[0].HasValue)
                    {
                        return cachedValues[0]!.Value;
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
                    await _redisDatabase.ScriptEvaluateAsync(
                        _storeFetchedValueIfFetchStoreOperationUnbroken,
                        [
                            GetShardedCounterForRedis(@namespace, name),
                            GetShardedCounterFetchStoreTokenForRedis(@namespace, name),
                        ],
                        [
                            fetchStoreToken!,
                            total,
                        ]).ConfigureAwait(false);
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
            var names = name.ToHashSet();

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
                    string? fetchStoreToken = null;
                    if (_redisDatabase != null)
                    {
                        // Load all of them at once.
                        fetchStoreToken = Guid.NewGuid().ToString();
                        notInRedisCache = new();
                        var futuresOrdered = futures.ToArray();
                        var redisKeys = new RedisKey[futuresOrdered.Length];
                        for (int i = 0; i < futuresOrdered.Length; i++)
                        {
                            redisKeys[i] = GetShardedCounterForRedis(@namespace, futuresOrdered[i].Key);
                        }
                        var shardCaches = RedisResultToArrayOfNumbers(await _redisDatabase.ScriptEvaluateAsync(
                            _loadExistingOrFlagFetchStoreOperation,
                            futuresOrdered.SelectMany(kv => new[]
                            {
                                GetShardedCounterForRedis(@namespace, kv.Key),
                                GetShardedCounterFetchStoreTokenForRedis(@namespace, kv.Key)
                            }).ToArray(),
                            [
                                new RedisValue(fetchStoreToken)
                            ]).ConfigureAwait(false));
                        for (int i = 0; i < futuresOrdered.Length; i++)
                        {
                            var shardCache = shardCaches[i];
                            var future = futuresOrdered[i];
                            if (shardCache.HasValue)
                            {
                                future.Value.SetValue(shardCache.Value);
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
                                    i => keyFactory.CreateKey(GetShardedCounterIndexForFirestore(name, i)),
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
                                                    await _redisDatabase.ScriptEvaluateAsync(
                                                        _storeFetchedValueIfFetchStoreOperationUnbroken,
                                                        [
                                                            GetShardedCounterForRedis(@namespace, waiting.Name),
                                                            GetShardedCounterFetchStoreTokenForRedis(@namespace, waiting.Name),
                                                        ],
                                                        [
                                                            fetchStoreToken,
                                                            total,
                                                        ]).ConfigureAwait(false);
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
                var key = keyFactory.CreateKey(GetShardedCounterIndexForFirestore(name, index));

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
                        await _redisDatabase.ScriptEvaluateAsync(
                            _adjustOrBreakFetchStoreOperation,
                            [
                                GetShardedCounterForRedis(@namespace, name),
                                GetShardedCounterFetchStoreTokenForRedis(@namespace, name),
                            ],
                            [
                                modifier,
                            ]).ConfigureAwait(false);
                    }
                };
            }
        }
    }
}
