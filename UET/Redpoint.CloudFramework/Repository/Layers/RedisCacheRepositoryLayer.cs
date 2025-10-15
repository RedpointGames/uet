namespace Redpoint.CloudFramework.Repository.Layers
{
    using Google.Cloud.Datastore.V1;
    using Google.Type;
    using Microsoft.Extensions.Caching.Distributed;
    using NodaTime;
    using Redpoint.Collections;
    using Redpoint.CloudFramework.Metric;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Expression;
    using Redpoint.CloudFramework.Repository.Converters.Model;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Tracing;
    using StackExchange.Redis;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static Google.Cloud.Datastore.V1.Key.Types;
    using Redpoint.Concurrency;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.Hashing;
    using System.Text.Json.Serialization;
    using System.Text.Json;

    internal partial class RedisCacheRepositoryLayer : IRedisCacheRepositoryLayer
    {
        private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;
        private readonly IConnectionMultiplexer _redis;
        private readonly IInstantTimestampConverter _instantTimestampConverter;
        private readonly IExpressionConverter _expressionConverter;
        private readonly IModelConverter<string> _jsonConverter;
        private readonly IMetricService _metricService;
        private readonly IManagedTracer _managedTracer;

        private const string _cacheQueries = "rcf/cache/queries";
        private const string _cacheLookups = "rcf/cache/lookups";
        private const string _cacheInvalidations = "rcf/cache/invalidations";

        public RedisCacheRepositoryLayer(
            IDatastoreRepositoryLayer datastoreRepositoryLayer,
            IDistributedCache distributedCache,
            IConnectionMultiplexer redis,
            IInstantTimestampConverter instantTimestampConverter,
            IExpressionConverter expressionConverter,
            IModelConverter<string> jsonConverter,
            IMetricService metricService,
            IManagedTracer managedTracer)
        {
            _datastoreRepositoryLayer = datastoreRepositoryLayer;
            _redis = redis;
            _instantTimestampConverter = instantTimestampConverter;
            _expressionConverter = expressionConverter;
            _jsonConverter = jsonConverter;
            _metricService = metricService;
            _managedTracer = managedTracer;
            _datastoreRepositoryLayer.OnNonTransactionalEntitiesModified.Add(this.ClearEntitiesFromCache);
        }

        private const string _purgeQueries = @"
local hashes = redis.call('SMEMBERS', KEYS[1])
local cacheKeys = {}
local queriesCleared = 0
for i, hash in ipairs(hashes) do
    local key_queryCache = 'QUERYCACHE:' .. hash
    local key_queryRefCount = 'QUERYRC:' .. hash
    local key_queryWriterCount = 'QUERYWC:' .. hash
    local key_queryData = 'QUERYDATA:' .. hash

    if redis.call('EXISTS', key_queryWriterCount) > 0 then
        -- Someone is writing into this cache value right now, but we've already invalidated
        -- the data they've partially pulled. Tell them their results are invalid.
        -- They will clean up their partially written entries.
        redis.call('SET', key_queryWriterCount, 'INVALIDATED')
    else
        local readers = tonumber(redis.call('GET', key_queryRefCount))
        if readers == nil then
            readers = 0
        end
        if readers > 0 then
            -- There are current readers. Just delete the QUERYCACHE, and leave the
            -- data to be cleaned up the by readers.
            table.insert(cacheKeys, key_queryCache)
        else
            -- There are no current readers, we can just purge it all.
            table.insert(cacheKeys, key_queryCache)
            table.insert(cacheKeys, key_queryRefCount)
            table.insert(cacheKeys, key_queryWriterCount)
            table.insert(cacheKeys, key_queryData)
        end
    end

    queriesCleared = queriesCleared + 1
end
if table.getn(cacheKeys) > 0 then
    for i = 1, table.getn(cacheKeys), 1000 do
        local e = math.min(table.getn(cacheKeys), i + 1000 - 1)
        local unlinkBatch = {}
        for a = i, e do
            table.insert(unlinkBatch, cacheKeys[a])
        end
        redis.call('UNLINK', unpack(unlinkBatch))
    end
end
return queriesCleared
";

        private const string _purgeColumns = @"
local queriesCleared = 0
for x, key in ipairs(KEYS) do
    local hashes = redis.call('SMEMBERS', key)
    local cacheKeys = {}
    for i, hash in ipairs(hashes) do
        local key_queryCache = 'QUERYCACHE:' .. hash
        local key_queryRefCount = 'QUERYRC:' .. hash
        local key_queryWriterCount = 'QUERYWC:' .. hash
        local key_queryData = 'QUERYDATA:' .. hash

        if redis.call('EXISTS', key_queryWriterCount) > 0 then
            -- Someone is writing into this cache value right now, but we've already invalidated
            -- the data they've partially pulled. Tell them their results are invalid.
            -- They will clean up their partially written entries.
            redis.call('SET', key_queryWriterCount, 'INVALIDATED')
        else
            local readers = tonumber(redis.call('GET', key_queryRefCount))
            if readers == nil then
                readers = 0
            end
            if readers > 0 then
                -- There are current readers. Just delete the QUERYCACHE, and leave the
                -- data to be cleaned up the by readers.
                table.insert(cacheKeys, key_queryCache)
            else
                -- There are no current readers, we can just purge it all.
                table.insert(cacheKeys, key_queryCache)
                table.insert(cacheKeys, key_queryRefCount)
                table.insert(cacheKeys, key_queryWriterCount)
                table.insert(cacheKeys, key_queryData)
            end
        end

        queriesCleared = queriesCleared + 1
    end
    if table.getn(cacheKeys) > 0 then
        for i = 1, table.getn(cacheKeys), 1000 do
            local e = math.min(table.getn(cacheKeys), i + 1000 - 1)
            local unlinkBatch = {}
            for a = i, e do
                table.insert(unlinkBatch, cacheKeys[a])
            end
            redis.call('UNLINK', unpack(unlinkBatch))
        end
    end
end
return queriesCleared
";

        private async Task ClearEntitiesFromCache(EntitiesModifiedEventArgs ev, CancellationToken cancellationToken)
        {
            using (_managedTracer.StartSpan($"db.rediscache.clear_entities_from_cache"))
            {
                var db = _redis.GetDatabase();

                // Clear simple cache keys.
                var keys = ev.Keys.Select(GetSimpleCacheKey).ToArray();
                for (int i = 0; i < keys.Length; i += 50)
                {
                    var buffer = new RedisKey[(int)Math.Min(i + 50, keys.Length - i)];
                    for (int x = 0; x < buffer.Length; x++)
                    {
                        buffer[x] = new RedisKey(keys[i + x]);
                    }
                    var removedCount = await db.KeyDeleteAsync(buffer).ConfigureAwait(false);
                    if (ev.Metrics != null)
                    {
                        ev.Metrics.CacheQueriesFlushed += removedCount;
                    }
                }

                // Clear complex caches.
                foreach (var key in ev.Keys.Select(GetSimpleCachedInKey))
                {
                    using (_managedTracer.StartSpan("db.rediscache.cache.purge_queries", $"{key}"))
                    {
                        var queriesFlushed = await db.ScriptEvaluateAsync(_purgeQueries, new[] { new RedisKey(key) }).ConfigureAwait(false);
                        if (ev.Metrics != null)
                        {
                            ev.Metrics.CacheQueriesFlushed += ((long)queriesFlushed);
                        }
                    }
                }
            }
        }

        private string SerializePathElement(PathElement pe)
        {
            var kind = pe.Kind.Contains('-', StringComparison.Ordinal) ? Convert.ToBase64String(Encoding.UTF8.GetBytes(pe.Kind)) : pe.Kind;
            if (pe.IdTypeCase == PathElement.IdTypeOneofCase.None)
            {
                return $"{kind}-none";
            }
            else if (pe.IdTypeCase == PathElement.IdTypeOneofCase.Id)
            {
                return $"{kind}-id-{pe.Id}";
            }
            else if (pe.IdTypeCase == PathElement.IdTypeOneofCase.Name)
            {
                return $"{kind}-name-{Convert.ToBase64String(Encoding.UTF8.GetBytes(pe.Name))}";
            }
            throw new NotImplementedException();
        }

        private string GetSimpleCacheKey(Key key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.PartitionId == null) throw new ArgumentNullException("key.PartitionId");
            if (key.PartitionId.ProjectId == null) throw new ArgumentNullException("key.PartitionId.ProjectId");
            if (key.PartitionId.NamespaceId == null) throw new ArgumentNullException("key.PartitionId.NamespaceId");
            if (key.Path == null) throw new ArgumentNullException("key.Path");
            return $"KEY:{key.PartitionId.ProjectId}/{key.PartitionId.NamespaceId}:{string.Join(":", key.Path.Select(SerializePathElement))}";
        }

        private string GetSimpleCachedInKey(Key key)
        {
            ArgumentNullException.ThrowIfNull(key);
            if (key.PartitionId == null) throw new ArgumentNullException("key.PartitionId");
            if (key.PartitionId.ProjectId == null) throw new ArgumentNullException("key.PartitionId.ProjectId");
            if (key.PartitionId.NamespaceId == null) throw new ArgumentNullException("key.PartitionId.NamespaceId");
            if (key.Path == null) throw new ArgumentNullException("key.Path");
            return $"KEYCACHEDIN:{key.PartitionId.ProjectId}/{key.PartitionId.NamespaceId}:{string.Join(":", key.Path.Select(SerializePathElement))}";
        }

        private class ComplexCacheKeyFilterJson
        {
            [JsonPropertyName("field"), JsonPropertyOrder(1)]
            public required string Field { get; set; }

            [JsonPropertyName("op"), JsonPropertyOrder(2)]
            public required string Op { get; set; }

            [JsonPropertyName("value"), JsonPropertyOrder(3)]
            public required string Value { get; set; }
        }

        private class ComplexCacheKeySortJson
        {
            [JsonPropertyName("field"), JsonPropertyOrder(1)]
            public required string Field { get; set; }

            [JsonPropertyName("direction"), JsonPropertyOrder(2)]
            public required string Direction { get; set; }
        }

        private class ComplexCacheKeyGeoJson
        {
            [JsonPropertyName("field"), JsonPropertyOrder(1)]
            public required string Field { get; set; }

            [JsonPropertyName("op"), JsonPropertyOrder(2)]
            public required string Op { get; set; }

            [JsonPropertyName("centerPointLat"), JsonPropertyOrder(3)]
            public required double CenterPointLat { get; set; }

            [JsonPropertyName("centerPointLng"), JsonPropertyOrder(4)]
            public required double CenterPointLng { get; set; }

            [JsonPropertyName("distanceKm"), JsonPropertyOrder(5)]
            public required double DistanceKm { get; set; }
        }

        private class ComplexCacheKeyJson
        {
            [JsonPropertyName("namespace"), JsonPropertyOrder(1)]
            public string? Namespace { get; set; }

            [JsonPropertyName("kind"), JsonPropertyOrder(2)]
            public string? Kind { get; set; }

            [JsonPropertyName("filter"), JsonPropertyOrder(3)]
            public ComplexCacheKeyFilterJson[]? Filter { get; set; }

            [JsonPropertyName("sort"), JsonPropertyOrder(4)]
            public ComplexCacheKeySortJson[]? Sort { get; set; }

            [JsonPropertyName("geo"), JsonPropertyOrder(5)]
            public ComplexCacheKeyGeoJson? Geo { get; set; }

            [JsonPropertyName("limit"), JsonPropertyOrder(5)]
            public int? Limit { get; set; }
        }

        [JsonSerializable(typeof(ComplexCacheKeyJson))]
        [JsonSerializable(typeof(bool))]
        [JsonSerializable(typeof(long))]
        private partial class RedisCacheJsonSerializerContext : JsonSerializerContext
        {
        }

        private Task<(string cacheHash, string[] columns)> GetComplexCacheHashAndColumns<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.get_complex_cache_hash_and_columns", $"{@namespace},{typeof(T).Name}"))
            {
                GeoQueryParameters<T>? geoQuery = null;
                var referenceModel = new T();
                var hasAncestorQuery = false;
                var filter = _expressionConverter.SimplifyFilter(_expressionConverter.ConvertExpressionToFilter(where.Body, where.Parameters[0], referenceModel, ref geoQuery, ref hasAncestorQuery));
                var sort = order == null ? null : _expressionConverter.ConvertExpressionToOrder(order.Body, order.Parameters[0], referenceModel, ref geoQuery)?.ToList();

                Filter[] filters;
                if (filter == null)
                {
                    filters = Array.Empty<Filter>();
                }
                else
                {
                    switch (filter.FilterTypeCase)
                    {
                        case Filter.FilterTypeOneofCase.CompositeFilter:
                            filters = filter.CompositeFilter.Filters.ToArray();
                            break;
                        case Filter.FilterTypeOneofCase.PropertyFilter:
                            filters = new[] { filter };
                            break;
                        case Filter.FilterTypeOneofCase.None:
                        default:
                            filters = Array.Empty<Filter>();
                            break;
                    }
                }

                var columns = new HashSet<string>();
                if (filters.Length == 0)
                {
                    columns.Add($"KEYALL:{@namespace}:{referenceModel.GetKind()}");
                }
                foreach (var f in filters)
                {
                    columns.Add($"KEYCOLUMN:{@namespace}:{referenceModel.GetKind()}:{f.PropertyFilter.Property.Name}");
                }
                if (sort != null)
                {
                    foreach (var s in sort)
                    {
                        columns.Add($"KEYCOLUMN:{@namespace}:{referenceModel.GetKind()}:{s.Property.Name}");
                    }
                }

                var cacheKeyJson = new ComplexCacheKeyJson
                {
                    Namespace = @namespace,
                    Kind = referenceModel.GetKind(),
                    Filter = filters.OrderBy(x => x.PropertyFilter.Property.Name).Select(x => new ComplexCacheKeyFilterJson
                    {
                        Field = x.PropertyFilter.Property.Name,
                        Op = RedisCacheRepositoryLayer.SerializeOp(x.PropertyFilter.Op),
                        Value = SerializeValue(x.PropertyFilter.Value),
                    }).ToArray(),
                    Sort = sort == null ? null : sort.Select(x => new ComplexCacheKeySortJson
                    {
                        Field = x.Property.Name,
                        Direction = x.Direction == PropertyOrder.Types.Direction.Ascending ? "asc" : "desc",
                    }).ToArray(),
                    Geo = geoQuery == null ? null : new ComplexCacheKeyGeoJson
                    {
                        Field = geoQuery.GeoFieldName,
                        CenterPointLat = geoQuery.CenterPoint.Latitude,
                        CenterPointLng = geoQuery.CenterPoint.Longitude,
                        DistanceKm = geoQuery.DistanceKm,
                        Op = "within-km",
                    },
                    Limit = limit,
                };
                var cacheHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(cacheKeyJson, RedisCacheJsonSerializerContext.Default.ComplexCacheKeyJson)))).ToLowerInvariant();
                return Task.FromResult((cacheHash, columns.ToArray()));
            }
        }

        private static string SerializeOp(PropertyFilter.Types.Operator op)
        {
            switch (op)
            {
                case PropertyFilter.Types.Operator.Unspecified:
                    return "unspecified";
                case PropertyFilter.Types.Operator.LessThan:
                    return "lt";
                case PropertyFilter.Types.Operator.LessThanOrEqual:
                    return "le";
                case PropertyFilter.Types.Operator.GreaterThan:
                    return "gt";
                case PropertyFilter.Types.Operator.GreaterThanOrEqual:
                    return "ge";
                case PropertyFilter.Types.Operator.HasAncestor:
                    return "has-ancestor";
                case PropertyFilter.Types.Operator.Equal:
                    return "eq";
                case PropertyFilter.Types.Operator.In:
                    return "in";
                case PropertyFilter.Types.Operator.NotIn:
                    return "not-in";
                default:
                    throw new NotImplementedException();
            }
        }

        private string SerializeValue(Value value)
        {
            switch (value.ValueTypeCase)
            {
                case Value.ValueTypeOneofCase.NullValue:
                    return "null";
                case Value.ValueTypeOneofCase.None:
                    return "none";
                case Value.ValueTypeOneofCase.BooleanValue:
                    return "bool:" + JsonSerializer.Serialize(value.BooleanValue, RedisCacheJsonSerializerContext.Default.Boolean);
                case Value.ValueTypeOneofCase.IntegerValue:
                    return "int:" + JsonSerializer.Serialize(value.IntegerValue, RedisCacheJsonSerializerContext.Default.Int64);
                case Value.ValueTypeOneofCase.DoubleValue:
                    return "double:" + JsonSerializer.Serialize(value.DoubleValue, RedisCacheJsonSerializerContext.Default.Double);
                case Value.ValueTypeOneofCase.KeyValue:
                    return $"key:{value.KeyValue.PartitionId.ProjectId}/{value.KeyValue.PartitionId.NamespaceId}/{string.Join("/", value.KeyValue.Path.Select(SerializePathElement))}";
                case Value.ValueTypeOneofCase.EntityValue:
                    throw new NotSupportedException();
                case Value.ValueTypeOneofCase.GeoPointValue:
                    return $"geo:{value.GeoPointValue.Latitude}:{value.GeoPointValue.Longitude}";
                case Value.ValueTypeOneofCase.ArrayValue:
                    return "array:" + Hash.XxHash64(string.Join(',', value.ArrayValue.Values.Select(SerializeValue)), Encoding.UTF8);
                case Value.ValueTypeOneofCase.TimestampValue:
                    return $"ts:{_instantTimestampConverter.FromDatastoreValueToNodaTimeInstant(value.TimestampValue)!.Value.ToUnixTimeTicks()}";
                case Value.ValueTypeOneofCase.StringValue:
                    return $"string:" + JsonSerializer.Serialize(value.StringValue, RedisCacheJsonSerializerContext.Default.String);
                case Value.ValueTypeOneofCase.BlobValue:
                    throw new NotSupportedException();
                default:
                    throw new NotImplementedException();
            }
        }

        public AsyncEvent<EntitiesModifiedEventArgs> OnNonTransactionalEntitiesModified => _datastoreRepositoryLayer.OnNonTransactionalEntitiesModified;

        private const string _tryObtainComplexCache = @"
if redis.call('EXISTS', KEYS[4]) > 0 then
    -- There is another writer writing into this cache value. Do not conflict
    -- with it.
    return 'nocache-nostore'
end

if redis.call('EXISTS', KEYS[1]) == 0 then
    -- The key that controls expiry doesn't exist. See if we should clean it up
    -- in case the associated data is stale.
    if redis.call('EXISTS', KEYS[3]) == 0 then
        redis.call('UNLINK', KEYS[2])
    end

    -- No cached data available, or it's stale.
    local readers = tonumber(redis.call('GET', KEYS[2]))
    if readers == nil then
        readers = 0
    end
    if readers > 0 then
        -- The cached data that is present is stale, and we can't store our
        -- fresh results because there's another reader currently using the
        -- cache.
        return 'nocache-nostore'
    else
        -- There's no cached data, or it's stale with no readers. Read from
        -- Datastore and write into the cache. Tell other queries that we
        -- now have an exclusive writer lock on this cache entry.
        redis.call('SET', KEYS[4], 'WRITING')
        return 'nocache-store'
    end
end

-- Increment the reference counter to prevent the expiry key from causing the
-- data to be released. Also, mark the keys for persistence while we're reading
-- cache data so they don't get deleted.
redis.call('INCR', KEYS[2])
redis.call('PERSIST', KEYS[1])
redis.call('PERSIST', KEYS[2])
redis.call('PERSIST', KEYS[3])

-- Cached data is available and we obtained a handle to iterate on it.
return 'cache'
";
        private const string _releaseComplexCache = @"
-- Decrement the cache reference counter.
redis.call('DECR', KEYS[2])

-- If the cache reference counter is at 0, then there are no operations currently
-- reading from this cache data, so turn back on expiry.
local readers = tonumber(redis.call('GET', KEYS[2]))
if readers == 0 then
    redis.call('EXPIRE', KEYS[1], 120)
    redis.call('UNLINK', KEYS[2])
    redis.call('EXPIRE', KEYS[3], 240)
end
return readers
";
        private const string _writeCachedEntityIntoCache = @"
if redis.call('GET', KEYS[2]) ~= ARGV[2] then
    -- Read data is now stale, do not write to cache.
    return
end
for i = 3, table.getn(ARGV) do
    -- Add the entity JSON to the cache data.
    redis.call('LPUSH', KEYS[1], ARGV[i])

    -- Add the query hash to the entity's cache key, which allows the query
    -- data to be purged when the entity is modified.
    redis.call('SADD', KEYS[i], ARGV[1])
end
";
        private const string _finalizeCacheWriting = @"
-- Check to see if the read data was invalidated through a concurrent write.
if redis.call('GET', KEYS[5]) ~= ARGV[2] then    
    redis.call('UNLINK', KEYS[1], KEYS[2], KEYS[3], KEYS[4])
    return 'invalidated'
end

-- Check to see if our write was explicitly invalidated.
if redis.call('GET', KEYS[4]) == 'INVALIDATED' then
    redis.call('UNLINK', KEYS[1], KEYS[2], KEYS[3], KEYS[4])
    return 'invalidated'
end

-- Add our query to the specified column keys.
for i = 6, table.getn(KEYS) do
    redis.call('SADD', KEYS[i], ARGV[1])
end

-- Our write was finalized properly. Set up the expiries.
redis.call('SETEX', KEYS[1], 120, '0')
redis.call('EXPIRE', KEYS[2], 240)
redis.call('EXPIRE', KEYS[3], 240)

-- Release our writer lock.
redis.call('UNLINK', KEYS[4])
return 'written'
";
        private const string _writeSingleCachedEntityIntoCache = @"
if redis.call('GET', KEYS[2]) ~= ARGV[2] then
    -- Read data is now stale, do not write to cache.
    return 'invalidated'
end

-- Add the entity to the cache.
redis.call('SET', KEYS[1], ARGV[1])
return 'written'
";

        private async Task<(RedisKey key, string lastWrite)> GetLastWriteAsync(IDatabase cache, string @namespace, IModel model)
        {
            string queryLastWriteValue = "0";
            var queryLastWriteKey = new RedisKey($"LASTWRITE:{model.GetKind()}");
            using (_managedTracer.StartSpan("db.rediscache.load.get_last_write", $"{@namespace},{model.GetType().Name}"))
            {
                var lastWriteValue = await cache.StringGetAsync(queryLastWriteKey).ConfigureAwait(false);
                if (lastWriteValue.HasValue)
                {
                    queryLastWriteValue = (string)lastWriteValue!;
                }
                else
                {
                    queryLastWriteValue = "0";
                }
            }
            return (queryLastWriteKey, queryLastWriteValue);
        }

        private static async Task IncrementLastWriteAsync(IDatabase cache, IModel model)
        {
            await cache.StringIncrementAsync($"LASTWRITE:{model.GetKind()}").ConfigureAwait(false);
        }

        private static async Task IncrementLastWriteAsync(IDatabase cache, string kind)
        {
            await cache.StringIncrementAsync($"LASTWRITE:{kind}").ConfigureAwait(false);
        }

        public IBatchedAsyncEnumerable<T> QueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
            => BatchedQueryAsync(
                @namespace,
                where,
                order,
                limit,
                transaction,
                metrics,
                cancellationToken).AsBatchedAsyncEnumerable();

        private async IAsyncEnumerable<IReadOnlyList<T>> BatchedQueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.query", $"{@namespace},{typeof(T).Name}"))
            {
                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    if (transaction != null)
                    {
                        // Transactional queries must hit Datastore so that Datastore can enforce transactionality. If
                        // an entity that was read is written to before the current transaction finishes, Datastore will
                        // force our application to retry the transaction. If we were to hit the cache in these scenarios
                        // Datastore would not be able to detect concurrency issues and would not throw the appropriate
                        // exception.
                        if (metrics != null)
                        {
                            metrics.CacheDidRead = false;
                            metrics.CacheDidWrite = false;
                            metrics.CacheCompatible = false;
                        }
                        await foreach (var batch in _datastoreRepositoryLayer.QueryAsync<T>(
                            @namespace,
                            where,
                            order,
                            limit,
                            transaction,
                            metrics,
                            cancellationToken).AsBatches().ConfigureAwait(false))
                        {
                            yield return batch;
                        }
                    }
                    else
                    {
                        var (cacheHash, columns) = await GetComplexCacheHashAndColumns(@namespace, where, order, limit).ConfigureAwait(false);

                        var queryCache = new RedisKey($"QUERYCACHE:{cacheHash}");
                        var queryRefCount = new RedisKey($"QUERYRC:{cacheHash}");
                        var queryWriterCount = new RedisKey($"QUERYWC:{cacheHash}");
                        var queryData = new RedisKey($"QUERYDATA:{cacheHash}");

                        if (metrics != null)
                        {
                            metrics.CacheCompatible = true;
                            metrics.CacheHash = cacheHash;
                        }

                        var cache = _redis.GetDatabase();
                        var (queryLastWriteKey, queryLastWriteValue) = await GetLastWriteAsync(cache, @namespace, new T()).ConfigureAwait(false);
                        RedisResult obtainCacheResult;
                        using (_managedTracer.StartSpan("db.rediscache.cache.try_obtain_complex_cache", $"{@namespace},{typeof(T).Name}"))
                        {
                            obtainCacheResult = await cache.ScriptEvaluateAsync(_tryObtainComplexCache, keys: new[]
                            {
                                queryCache,
                                queryRefCount,
                                queryData,
                                queryWriterCount
                            }).ConfigureAwait(false);
                        }
                        await _metricService.AddPoint(_cacheQueries, 1, null, new Dictionary<string, string?>
                        {
                            { "kind", (new T()).GetKind() },
                            { "namespace", @namespace },
                            { "result", (string)obtainCacheResult! },
                        }).ConfigureAwait(false);
                        switch ((string)obtainCacheResult!)
                        {
                            case "cache":
                                {
                                    if (metrics != null)
                                    {
                                        metrics.CacheDidRead = true;
                                    }
                                    try
                                    {
                                        // Read the entity data from Redis instead of Datastore.
                                        var length = await cache.ListLengthAsync(queryData).ConfigureAwait(false);
                                        for (var start = 0; start < length; start += 200)
                                        {
                                            var stop = Math.Min(start + 200, length);
                                            var results = await cache.ListRangeAsync(queryData, start, stop - 1).ConfigureAwait(false);
                                            var batchResults = new List<T>();
                                            foreach (var result in results)
                                            {
                                                var model = _jsonConverter.From<T>(@namespace, result!);
                                                if (model != null)
                                                {
                                                    batchResults.Add(model);
                                                }
                                            }
                                            yield return batchResults;
                                        }
                                    }
                                    finally
                                    {
                                        using (_managedTracer.StartSpan("db.rediscache.cache.release_complex_cache", $"{@namespace},{typeof(T).Name}"))
                                        {
                                            await cache.ScriptEvaluateAsync(_releaseComplexCache, keys: new[]
                                            {
                                                queryCache,
                                                queryRefCount,
                                                queryData,
                                            }).ConfigureAwait(false);
                                        }
                                    }
                                }
                                break;
                            case "nocache-store":
                                {
                                    await cache.KeyDeleteAsync(new[] {
                                        queryCache,
                                        queryRefCount,
                                        queryData,
                                    }).ConfigureAwait(false);

                                    // TODO: We should have a better strategy for partial queries; that is queries that have things like
                                    // FirstOrDefaultAsync() applied to them. We currently pull all of the data from Datastore that would
                                    // match the query, even if something like FirstOrDefault stops elements being pulled for the caller.
                                    //
                                    // This is because our caching logic can't yet handle a partial dataset in the cache. A future strategy
                                    // for handling partial queries (e.g. FirstOrDefaultAsync) without pulling all of the data during
                                    // "nocache-store":
                                    //
                                    // - If we fall into the finally here mark the result set as partial.
                                    // - If we're pulling a partial result set in the "cache" block above, and we go beyond the entities
                                    //   available in Redis, we then run a Datastore query.
                                    // - We have to try to obtain a CACHEWC lock on the data set we're reading cached results for. That is,
                                    //   there can't be any other readers that are also concurrently reading the cached data.
                                    // - If there are no other readers, then "cache" becomes "nocache-store", but excluding entities we
                                    //   were already able to enumerate from Redis. We store further entities as we come across them.
                                    // - If there are other readers, then "cache" becomes "nocache-nostore", but excluding entities we
                                    //   were already able to enumerate from Redis.

                                    var pullSemaphore = new SemaphoreSlim(0);
                                    var pullBatches = new ConcurrentQueue<IReadOnlyList<T>>();
                                    Exception? pullException = null;
                                    var puller = Task.Run(async () =>
                                    {
                                        var didFinish = false;
                                        try
                                        {
                                            // There is no cache data for this query, and we're going to store it as we
                                            // read our data from Datastore.
                                            await foreach (var batch in _datastoreRepositoryLayer.QueryAsync<T>(
                                                @namespace,
                                                where,
                                                order,
                                                limit,
                                                transaction,
                                                metrics,
                                                CancellationToken.None).AsBatches().ConfigureAwait(false))
                                            {
                                                using (_managedTracer.StartSpan("db.rediscache.cache.batch_process", $"{@namespace},{typeof(T).Name}"))
                                                {
                                                    var keys = new List<RedisKey>
                                                    {
                                                        queryData,
                                                        queryLastWriteKey,
                                                    };
                                                    var values = new List<RedisValue>
                                                    {
                                                        new RedisValue(cacheHash),
                                                        (RedisValue)queryLastWriteValue,
                                                    };
                                                    foreach (var entity in batch)
                                                    {
                                                        var cachedEntity = _jsonConverter.To(@namespace, entity, false, null);

                                                        keys.Add(new RedisKey(GetSimpleCachedInKey(entity.Key)));
                                                        values.Add(new RedisValue(cachedEntity));
                                                    }

                                                    using (_managedTracer.StartSpan("db.rediscache.cache.write_cached_entity_to_cache", $"{@namespace},{typeof(T).Name}"))
                                                    {
                                                        await cache.ScriptEvaluateAsync(
                                                            _writeCachedEntityIntoCache,
                                                            keys.ToArray(),
                                                            values.ToArray()).ConfigureAwait(false);
                                                    }
                                                }

                                                using (_managedTracer.StartSpan("db.rediscache.cache.batch_emit", $"{@namespace},{typeof(T).Name}"))
                                                {
                                                    if (pullBatches != null)
                                                    {
                                                        pullBatches.Enqueue(batch);
                                                        pullSemaphore.Release();
                                                    }
                                                }
                                            }

                                            RedisResult finalizeResult;
                                            using (_managedTracer.StartSpan("db.rediscache.cache.finalize_cache_writing", $"{@namespace},{typeof(T).Name}"))
                                            {
                                                finalizeResult = await cache.ScriptEvaluateAsync(
                                                    _finalizeCacheWriting,
                                                    new[]
                                                    {
                                                        queryCache,
                                                        queryRefCount,
                                                        queryData,
                                                        queryWriterCount,
                                                        queryLastWriteKey,
                                                    }.Concat(columns.Select(x => new RedisKey(x))).ToArray(),
                                                    new[]
                                                    {
                                                        new RedisValue(cacheHash),
                                                        (RedisValue)queryLastWriteValue,
                                                    }).ConfigureAwait(false);
                                            }
                                            if (((string)finalizeResult!) != "invalidated")
                                            {
                                                if (metrics != null)
                                                {
                                                    metrics.CacheDidWrite = true;
                                                }
                                            }

                                            didFinish = true;
                                        }
                                        catch (Exception ex)
                                        {
                                            pullException = ex;
                                        }
                                        finally
                                        {
                                            // Not sure what state the cache is in because we might have partially written results.
                                            if (!didFinish)
                                            {
                                                await cache.KeyDeleteAsync(new[] {
                                                    queryCache,
                                                    queryRefCount,
                                                    queryData,
                                                    queryWriterCount
                                                }).ConfigureAwait(false);
                                            }
                                        }
                                    }, cancellationToken);

                                    try
                                    {
                                        while (!cancellationToken.IsCancellationRequested)
                                        {
                                            if (await Task.WhenAny(pullSemaphore.WaitAsync(cancellationToken), puller).ConfigureAwait(false) == puller)
                                            {
                                                // The puller has finished. Yield the remaining elements.
                                                while (pullBatches.TryDequeue(out IReadOnlyList<T>? next))
                                                {
                                                    cancellationToken.ThrowIfCancellationRequested();
                                                    yield return next;
                                                }
                                                yield break;
                                            }
                                            else
                                            {
                                                // We got another entity in the queue.
                                                while (!puller.IsCompleted)
                                                {
                                                    cancellationToken.ThrowIfCancellationRequested();
                                                    if (pullBatches.TryDequeue(out IReadOnlyList<T>? next))
                                                    {
                                                        yield return next;
                                                    }
                                                    else
                                                    {
                                                        // Queue contention; try again.
                                                        await Task.Yield();
                                                        continue;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    finally
                                    {
                                        // Wait for the puller to just push everything into the cache, and ask it to not store
                                        // into the concurrent queue for us.
                                        pullBatches = null;
                                        await puller.ConfigureAwait(false);
                                    }
                                }
                                break;
                            case "nocache-nostore":
                                {
                                    // There is another reader on the existing cache value at the moment, but
                                    // the contents of the cache is stale (because a write happened after the
                                    // other reader started reading, but before we started reading). We just go
                                    // straight to the database, and don't store our result in Redis (because
                                    // we need to wait for all readers to finish before storing a new cache value).
                                    await foreach (var batch in _datastoreRepositoryLayer.QueryAsync<T>(
                                        @namespace,
                                        where,
                                        order,
                                        limit,
                                        transaction,
                                        metrics,
                                        cancellationToken).AsBatches().ConfigureAwait(false))
                                    {
                                        yield return batch;
                                    }
                                }
                                break;
                        }
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.CacheElapsedMilliseconds = stopwatch!.ElapsedMilliseconds - metrics.DatastoreElapsedMilliseconds;
                    }
                }
            }
        }

        public async Task<PaginatedQueryResult<T>> QueryPaginatedAsync<T>(
            string @namespace,
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.query_paginated", $"{@namespace},{typeof(T).Name}"))
            {
                if (metrics != null)
                {
                    metrics.CacheCompatible = false;
                }
                return await _datastoreRepositoryLayer.QueryPaginatedAsync(
                    @namespace,
                    cursor,
                    limit,
                    where,
                    order,
                    transaction,
                    metrics,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public async Task<T?> LoadAsync<T>(
            string @namespace,
            Key key,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.load", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));
                ArgumentNullException.ThrowIfNull(key);

                if (metrics != null)
                {
                    metrics.CacheCompatible = true;
                }

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    if (transaction != null)
                    {
                        // Transactional loads must hit Datastore so that Datastore can enforce transactionality. If
                        // an entity that was read is written to before the current transaction finishes, Datastore will
                        // force our application to retry the transaction. If we were to hit the cache in these scenarios
                        // Datastore would not be able to detect concurrency issues and would not throw the appropriate
                        // exception.
                        if (metrics != null)
                        {
                            metrics.CacheDidRead = false;
                            metrics.CacheDidWrite = false;
                            metrics.CacheCompatible = false;
                        }
                        return await _datastoreRepositoryLayer.LoadAsync<T>(
                            @namespace,
                            key,
                            transaction,
                            metrics,
                            cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        // This value ensures that we don't write stale data to the 
                        // cache if there's been a write since we started running.
                        var cache = _redis.GetDatabase();
                        var (queryLastWriteKey, queryLastWriteValue) = await GetLastWriteAsync(cache, @namespace, new T()).ConfigureAwait(false);

                        var cacheKey = GetSimpleCacheKey(key);
                        var cacheEntity = await cache.StringGetAsync(cacheKey).ConfigureAwait(false);
                        if (cacheEntity.HasValue)
                        {
                            await _metricService.AddPoint(_cacheLookups, 1, null, new Dictionary<string, string?>
                            {
                                { "kind", (new T()).GetKind() },
                                { "namespace", @namespace },
                                { "result", "hit" },
                            }).ConfigureAwait(false);
                            if (metrics != null)
                            {
                                metrics.CacheDidRead = true;
                            }
                            var model = _jsonConverter.From<T>(
                                @namespace,
                                (string)cacheEntity!);
                            if (model != null)
                            {
                                return model;
                            }
                        }

                        var keyFactory = await _datastoreRepositoryLayer.GetKeyFactoryAsync<T>(@namespace, metrics, cancellationToken).ConfigureAwait(false);
                        var entity = await _datastoreRepositoryLayer.LoadAsync<T>(
                            @namespace,
                            key,
                            transaction,
                            metrics,
                            cancellationToken).ConfigureAwait(false);
                        cacheEntity = _jsonConverter.To(
                            @namespace,
                            entity,
                            false,
                            _ => keyFactory.CreateIncompleteKey());
                        RedisResult cacheResult;
                        using (_managedTracer.StartSpan("db.rediscache.load.write_cached_entity_to_cache", $"{@namespace},{typeof(T).Name}"))
                        {
                            cacheResult = await cache.ScriptEvaluateAsync(
                                _writeSingleCachedEntityIntoCache,
                                new RedisKey[]
                                {
                                    cacheKey,
                                    queryLastWriteKey,
                                },
                                new RedisValue[]
                                {
                                    new RedisValue(cacheEntity!),
                                    (RedisValue)queryLastWriteValue,
                                }).ConfigureAwait(false);
                        }
                        await _metricService.AddPoint(_cacheLookups, 1, null, new Dictionary<string, string?>
                        {
                            { "kind", (new T()).GetKind() },
                            { "namespace", @namespace },
                            { "result", "miss" },
                        }).ConfigureAwait(false);
                        if (((string)cacheResult!) != "invalidated")
                        {
                            if (metrics != null)
                            {
                                metrics.CacheDidWrite = true;
                            }
                        }
                        return entity;
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.CacheElapsedMilliseconds = stopwatch!.ElapsedMilliseconds - metrics.DatastoreElapsedMilliseconds;
                    }
                }
            }
        }

        public IBatchedAsyncEnumerable<KeyValuePair<Key, T?>> LoadAsync<T>(
            string @namespace,
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
            => BatchedLoadAsync<T>(
                @namespace,
                keys,
                transaction,
                metrics,
                cancellationToken).AsBatchedAsyncEnumerable();

        private async IAsyncEnumerable<IReadOnlyList<KeyValuePair<Key, T?>>> BatchedLoadAsync<T>(
            string @namespace,
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.load", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                if (metrics != null)
                {
                    metrics.CacheCompatible = true;
                }

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    if (transaction != null)
                    {
                        // Transactional loads must hit Datastore so that Datastore can enforce transactionality. If
                        // an entity that was read is written to before the current transaction finishes, Datastore will
                        // force our application to retry the transaction. If we were to hit the cache in these scenarios
                        // Datastore would not be able to detect concurrency issues and would not throw the appropriate
                        // exception.
                        if (metrics != null)
                        {
                            metrics.CacheDidRead = false;
                            metrics.CacheDidWrite = false;
                            metrics.CacheCompatible = false;
                        }

                        await foreach (var batch in _datastoreRepositoryLayer.LoadAsync<T>(
                            @namespace,
                            keys,
                            transaction,
                            metrics,
                            cancellationToken).AsBatches().ConfigureAwait(false))
                        {
                            yield return batch;
                        }
                    }
                    else
                    {
                        var keyFactory = await _datastoreRepositoryLayer.GetKeyFactoryAsync<T>(@namespace, metrics, cancellationToken).ConfigureAwait(false);

                        // This value ensures that we don't write stale data to the 
                        // cache if there's been a write since we started running.
                        var cache = _redis.GetDatabase();
                        var (queryLastWriteKey, queryLastWriteValue) = await GetLastWriteAsync(cache, @namespace, new T()).ConfigureAwait(false);

                        var cacheEvaluation = keys.SelectFastAwait(async key =>
                        {
                            if (key == null)
                            {
                                throw new ArgumentNullException(nameof(keys), "One or more keys passed to LoadAsync was null.");
                            }

                            var cacheKey = GetSimpleCacheKey(key);
                            var cacheEntity = await cache.StringGetAsync(cacheKey).ConfigureAwait(false);
                            return (key: key, cacheEntity: cacheEntity.HasValue ? (string)cacheEntity! : null);
                        });

                        var hits = 0;
                        var misses = 0;
                        var entities = cacheEvaluation
                            .Classify<(Key key, string? cacheEntity), KeyValuePair<Key, T?>>(x => x.cacheEntity != null ? "hit" : "miss")
                            .AndForClassification("hit", x =>
                            {
                                hits++;
                                if (metrics != null)
                                {
                                    metrics.CacheDidRead = true;
                                }
                                return new KeyValuePair<Key, T?>(
                                    x.key,
                                    _jsonConverter.From<T>(@namespace, x.cacheEntity!));
                            })
                            .AndForClassificationStream("miss", inputs =>
                            {
                                misses++;
                                return
                                    _datastoreRepositoryLayer.LoadAsync<T>(
                                        @namespace,
                                        inputs.Select(x => x.key),
                                        transaction,
                                        metrics,
                                        cancellationToken)
                                    .SelectFastAwait(async v =>
                                    {
                                        // Store in the cache as we get the results from Datastore.
                                        var cacheKey = GetSimpleCacheKey(v.Key);
                                        var cacheEntity = _jsonConverter.To(
                                            @namespace,
                                            v.Value,
                                            false,
                                            _ => keyFactory.CreateIncompleteKey());
                                        RedisResult cacheResult = await cache.ScriptEvaluateAsync(
                                            _writeSingleCachedEntityIntoCache,
                                            new RedisKey[]
                                            {
                                                cacheKey,
                                                queryLastWriteKey,
                                            },
                                            new RedisValue[]
                                            {
                                                new RedisValue(cacheEntity),
                                                (RedisValue)queryLastWriteValue,
                                            }).ConfigureAwait(false);
                                        if (((string)cacheResult!) != "invalidated")
                                        {
                                            if (metrics != null)
                                            {
                                                metrics.CacheDidWrite = true;
                                            }
                                        }
                                        return v;
                                    });
                            });
                        if (hits > 0)
                        {
                            await _metricService.AddPoint(_cacheLookups, hits, null, new Dictionary<string, string?>
                            {
                                { "kind", (new T()).GetKind() },
                                { "namespace", @namespace },
                                { "result", "hit" },
                            }).ConfigureAwait(false);
                        }
                        if (misses > 0)
                        {
                            await _metricService.AddPoint(_cacheLookups, misses, null, new Dictionary<string, string?>
                            {
                                { "kind", (new T()).GetKind() },
                                { "namespace", @namespace },
                                { "result", "hit" },
                            }).ConfigureAwait(false);
                        }

                        // @note: The classify API doesn't give us a good way of propagating the batches from
                        // Datastore yet; just emulate some batches here.
                        await foreach (var entity in entities.BatchInto(200).WithCancellation(cancellationToken))
                        {
                            yield return entity;
                        }
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.CacheElapsedMilliseconds = stopwatch!.ElapsedMilliseconds - metrics.DatastoreElapsedMilliseconds;
                    }
                }
            }
        }

        public async IAsyncEnumerable<KeyValuePair<Key, T?>> LoadAcrossNamespacesAsync<T>(
            IAsyncEnumerable<Key> keys,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.load_across_namespaces", $"{typeof(T).Name}"))
            {
                if (metrics != null)
                {
                    metrics.CacheCompatible = true;
                }

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    // This value ensures that we don't write stale data to the 
                    // cache if there's been a write since we started running.
                    var cache = _redis.GetDatabase();
                    var (queryLastWriteKey, queryLastWriteValue) = await GetLastWriteAsync(cache, "(cross-namespace)", new T()).ConfigureAwait(false);

                    var cacheEvaluation = keys.SelectFastAwait(async key =>
                    {
                        if (key == null)
                        {
                            throw new ArgumentNullException(nameof(keys), "One or more keys passed to LoadAcrossNamespacesAsync was null.");
                        }

                        var cacheKey = GetSimpleCacheKey(key);
                        var cacheEntity = await cache.StringGetAsync(cacheKey).ConfigureAwait(false);
                        return (key: key, cacheEntity: cacheEntity.HasValue ? (string)cacheEntity! : null);
                    });

                    var hits = 0;
                    var misses = 0;
                    var entities = cacheEvaluation
                        .Classify<(Key key, string? cacheEntity), KeyValuePair<Key, T?>>(x => x.cacheEntity != null ? "hit" : "miss")
                        .AndForClassification("hit", x =>
                        {
                            hits++;
                            if (metrics != null)
                            {
                                metrics.CacheDidRead = true;
                            }
                            return new KeyValuePair<Key, T?>(
                                x.key,
                                _jsonConverter.From<T>(x.key.PartitionId.NamespaceId, x.cacheEntity!));
                        })
                        .AndForClassificationStream("miss", inputs =>
                        {
                            misses++;
                            return
                                _datastoreRepositoryLayer.LoadAcrossNamespacesAsync<T>(
                                    inputs.Select(x => x.key),
                                    metrics,
                                    cancellationToken)
                                .SelectFastAwait(async v =>
                                {
                                    // Store in the cache as we get the results from Datastore.
                                    var keyFactory = await _datastoreRepositoryLayer.GetKeyFactoryAsync<T>(v.Key.PartitionId.NamespaceId, metrics, cancellationToken).ConfigureAwait(false);
                                    var cacheKey = GetSimpleCacheKey(v.Key);
                                    var cacheEntity = _jsonConverter.To(
                                        v.Key.PartitionId.NamespaceId,
                                        v.Value,
                                        false,
                                        _ => keyFactory.CreateIncompleteKey());
                                    RedisResult cacheResult = await cache.ScriptEvaluateAsync(
                                        _writeSingleCachedEntityIntoCache,
                                        new RedisKey[]
                                        {
                                            cacheKey,
                                            queryLastWriteKey,
                                        },
                                        new RedisValue[]
                                        {
                                            new RedisValue(cacheEntity),
                                            (RedisValue)queryLastWriteValue,
                                        }).ConfigureAwait(false);
                                    if (((string)cacheResult!) != "invalidated")
                                    {
                                        if (metrics != null)
                                        {
                                            metrics.CacheDidWrite = true;
                                        }
                                    }
                                    return v;
                                });
                        });
                    if (hits > 0)
                    {
                        await _metricService.AddPoint(_cacheLookups, hits, null, new Dictionary<string, string?>
                        {
                            { "kind", (new T()).GetKind() },
                            { "namespace", "(cross-namespace)" },
                            { "result", "hit" },
                        }).ConfigureAwait(false);
                    }
                    if (misses > 0)
                    {
                        await _metricService.AddPoint(_cacheLookups, misses, null, new Dictionary<string, string?>
                        {
                            { "kind", (new T()).GetKind() },
                            { "namespace", "(cross-namespace)" },
                            { "result", "hit" },
                        }).ConfigureAwait(false);
                    }

                    await foreach (var entity in entities.WithCancellation(cancellationToken))
                    {
                        yield return entity;
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.CacheElapsedMilliseconds = stopwatch!.ElapsedMilliseconds - metrics.DatastoreElapsedMilliseconds;
                    }
                }
            }
        }

        public async IAsyncEnumerable<T> CreateAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.create", $"{@namespace},{typeof(T).Name}"))
            {
                var columns = new HashSet<string>();
                try
                {
                    await foreach (var entity in _datastoreRepositoryLayer.CreateAsync<T>(
                        @namespace,
                        models,
                        transaction,
                        metrics,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (transaction == null)
                        {
                            columns.Add($"KEYALL:{@namespace}:{entity.GetKind()}");
                            foreach (var kv in entity.GetTypes())
                            {
                                columns.Add($"KEYCOLUMN:{@namespace}:{entity.GetKind()}:{kv.Key}");
                            }
                        }

                        yield return entity;
                    }
                }
                finally
                {
                    if (columns.Count > 0)
                    {
                        using (_managedTracer.StartSpan("db.rediscache.cache.purge_columns", $"{@namespace},{typeof(T).Name}"))
                        {
                            var db = _redis.GetDatabase();
                            await RedisCacheRepositoryLayer.IncrementLastWriteAsync(db, new T()).ConfigureAwait(false);
                            var queriesFlushed = await db.ScriptEvaluateAsync(_purgeColumns, columns.Select(x => new RedisKey(x)).ToArray()).ConfigureAwait(false);
                            if (metrics != null)
                            {
                                metrics.CacheQueriesFlushed += ((long)queriesFlushed);
                                await _metricService.AddPoint(_cacheInvalidations, ((long)queriesFlushed), null, new Dictionary<string, string?>
                                {
                                    { "kind", (new T()).GetKind() },
                                }).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        public async IAsyncEnumerable<T> UpsertAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.upsert", $"{@namespace},{typeof(T).Name}"))
            {
                var columns = new HashSet<string>();
                try
                {
                    await foreach (var entity in _datastoreRepositoryLayer.UpsertAsync<T>(
                        @namespace,
                        models,
                        transaction,
                        metrics,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (transaction == null)
                        {
                            columns.Add($"KEYALL:{@namespace}:{entity.GetKind()}");
                            foreach (var kv in entity.GetTypes())
                            {
                                // We must assume upserts are creates, therefore we don't compare values.
                                columns.Add($"KEYCOLUMN:{@namespace}:{entity.GetKind()}:{kv.Key}");
                            }
                        }

                        yield return entity;
                    }
                }
                finally
                {
                    if (columns.Count > 0)
                    {
                        using (_managedTracer.StartSpan("db.rediscache.cache.purge_columns", $"{@namespace},{typeof(T).Name}"))
                        {
                            var db = _redis.GetDatabase();
                            await RedisCacheRepositoryLayer.IncrementLastWriteAsync(db, new T()).ConfigureAwait(false);
                            var queriesFlushed = await db.ScriptEvaluateAsync(_purgeColumns, columns.Select(x => new RedisKey(x)).ToArray()).ConfigureAwait(false);
                            if (metrics != null)
                            {
                                metrics.CacheQueriesFlushed += ((long)queriesFlushed);
                                await _metricService.AddPoint(_cacheInvalidations, ((long)queriesFlushed), null, new Dictionary<string, string?>
                                {
                                    { "kind", (new T()).GetKind() },
                                }).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        public async IAsyncEnumerable<T> UpdateAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.update", $"{@namespace},{typeof(T).Name}"))
            {
                var columns = new HashSet<string>();
                try
                {
                    await foreach (var entity in _datastoreRepositoryLayer.UpdateAsync<T>(
                        @namespace,
                        models,
                        transaction,
                        metrics,
                        cancellationToken).ConfigureAwait(false))
                    {
                        if (transaction == null)
                        {
                            columns.Add($"KEYALL:{@namespace}:{entity.GetKind()}");

                            foreach (var kv in entity.GetTypes())
                            {
                                var wasColumnModified = false;
                                if (entity._originalData == null || !entity._originalData.TryGetValue(kv.Key, out object? oldValue))
                                {
                                    wasColumnModified = true;
                                }
                                else
                                {
                                    var newValue = entity.GetPropertyInfo(kv.Key)!.GetValue(entity);
                                    if (newValue == null)
                                    {
                                        wasColumnModified = oldValue != null;
                                    }
                                    else if (oldValue == null)
                                    {
                                        wasColumnModified = true;
                                    }
                                    else if (newValue.GetType() != oldValue.GetType())
                                    {
                                        wasColumnModified = true;
                                    }
                                    else
                                    {
                                        switch (oldValue)
                                        {
                                            case bool v:
                                                wasColumnModified = v != (bool)newValue;
                                                break;
                                            case string v:
                                                wasColumnModified = v != (string)newValue;
                                                break;
                                            case Instant v:
                                                wasColumnModified = v != (Instant)newValue;
                                                break;
                                            case long v:
                                                wasColumnModified = v != (long)newValue;
                                                break;
                                            case double v:
                                                wasColumnModified = v != (double)newValue;
                                                break;
                                            case LatLng v:
                                                wasColumnModified = v.Latitude != ((LatLng)newValue).Latitude || v.Longitude != ((LatLng)newValue).Longitude;
                                                break;
                                            case Key v:
                                                wasColumnModified = !v.Equals((Key)newValue);
                                                break;
                                            default:
                                                // Don't know how to compare this type yet, assume modified.
                                                wasColumnModified = true;
                                                break;
                                        }
                                    }
                                }

                                if (wasColumnModified)
                                {
                                    columns.Add($"KEYCOLUMN:{@namespace}:{entity.GetKind()}:{kv.Key}");
                                }
                            }
                        }

                        yield return entity;
                    }
                }
                finally
                {
                    if (columns.Count > 0)
                    {
                        using (_managedTracer.StartSpan("db.rediscache.cache.purge_columns", $"{@namespace},{typeof(T).Name}"))
                        {
                            var db = _redis.GetDatabase();
                            await RedisCacheRepositoryLayer.IncrementLastWriteAsync(db, new T()).ConfigureAwait(false);
                            var queriesFlushed = await db.ScriptEvaluateAsync(_purgeColumns, columns.Select(x => new RedisKey(x)).ToArray()).ConfigureAwait(false);
                            if (metrics != null)
                            {
                                metrics.CacheQueriesFlushed += ((long)queriesFlushed);
                                await _metricService.AddPoint(_cacheInvalidations, ((long)queriesFlushed), null, new Dictionary<string, string?>
                                {
                                    { "kind", (new T()).GetKind() },
                                }).ConfigureAwait(false);
                            }
                        }
                    }
                }
            }
        }

        public async Task DeleteAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.delete", $"{@namespace},{typeof(T).Name}"))
            {
                await _datastoreRepositoryLayer.DeleteAsync(
                    @namespace,
                    models,
                    transaction,
                    metrics,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        public Task<Key> AllocateKeyAsync<T>(
            string @namespace,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.allocate_key", $"{@namespace},{typeof(T).Name}"))
            {
                return _datastoreRepositoryLayer.AllocateKeyAsync<T>(@namespace, transaction, metrics, cancellationToken);
            }
        }

        public Task<KeyFactory> GetKeyFactoryAsync<T>(
            string @namespace,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.rediscache.get_key_factory", $"{@namespace},{typeof(T).Name}"))
            {
                return _datastoreRepositoryLayer.GetKeyFactoryAsync<T>(@namespace, metrics, cancellationToken);
            }
        }

        public Task<IModelTransaction> BeginTransactionAsync(
            string @namespace,
            TransactionMode mode,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            using (_managedTracer.StartSpan($"db.rediscache.begin_transaction", @namespace))
            {
                return _datastoreRepositoryLayer.BeginTransactionAsync(@namespace, mode, metrics, cancellationToken);
            }
        }

        public Task RollbackAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            using (_managedTracer.StartSpan($"db.rediscache.rollback", @namespace))
            {
                return _datastoreRepositoryLayer.RollbackAsync(@namespace, transaction, metrics, cancellationToken);
            }
        }

        public async Task CommitAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            using (_managedTracer.StartSpan($"db.rediscache.commit", @namespace))
            {
                await _datastoreRepositoryLayer.CommitAsync(@namespace, transaction, metrics, cancellationToken).ConfigureAwait(false);

                var db = _redis.GetDatabase();

                // For all the model types involved, prevent stale cache stores.
                foreach (var kind in transaction.ModifiedModels.Select(x => x.GetKind()).Distinct())
                {
                    await RedisCacheRepositoryLayer.IncrementLastWriteAsync(db, kind).ConfigureAwait(false); ;
                }

                // Clear simple cache keys.
                var keys = transaction.ModifiedModels.Select(x => GetSimpleCacheKey(x.Key)).ToArray();
                for (int i = 0; i < keys.Length; i += 50)
                {
                    var buffer = new RedisKey[(int)Math.Min(i + 50, keys.Length - i)];
                    for (int x = 0; x < buffer.Length; x++)
                    {
                        buffer[x] = new RedisKey(keys[i + x]);
                    }
                    var removedCount = await db.KeyDeleteAsync(buffer).ConfigureAwait(false);
                    if (metrics != null)
                    {
                        metrics.CacheQueriesFlushed += removedCount;
                        await _metricService.AddPoint(_cacheInvalidations, removedCount, null, new Dictionary<string, string?>
                        {
                            { "kind", "(transaction commit)" },
                        }).ConfigureAwait(false);
                    }
                }

                // Clear complex caches.
                foreach (var key in transaction.ModifiedModels.Select(x => GetSimpleCachedInKey(x.Key)))
                {
                    using (_managedTracer.StartSpan("db.rediscache.cache.purge_queries", $"{key}"))
                    {
                        var queriesFlushed = await db.ScriptEvaluateAsync(_purgeQueries, new[] { new RedisKey(key) }).ConfigureAwait(false);
                        if (metrics != null)
                        {
                            metrics.CacheQueriesFlushed += ((long)queriesFlushed);
                            await _metricService.AddPoint(_cacheInvalidations, ((long)queriesFlushed), null, new Dictionary<string, string?>
                            {
                                { "kind", "(transaction commit)" },
                            }).ConfigureAwait(false);
                        }
                    }
                }

                // Clear column keys.
                var columns = new HashSet<string>();
                foreach (var entity in transaction.ModifiedModels)
                {
                    columns.Add($"KEYALL:{@namespace}:{entity.GetKind()}");

                    foreach (var kv in entity.GetTypes())
                    {
                        var wasColumnModified = false;
                        if (entity._originalData == null || !entity._originalData.TryGetValue(kv.Key, out object? oldValue))
                        {
                            wasColumnModified = true;
                        }
                        else
                        {
                            var newValue = entity.GetPropertyInfo(kv.Key)!.GetValue(entity);
                            if (newValue == null)
                            {
                                wasColumnModified = oldValue != null;
                            }
                            else if (oldValue == null)
                            {
                                wasColumnModified = true;
                            }
                            else if (newValue.GetType() != oldValue.GetType())
                            {
                                wasColumnModified = true;
                            }
                            else
                            {
                                switch (oldValue)
                                {
                                    case bool v:
                                        wasColumnModified = v != (bool)newValue;
                                        break;
                                    case string v:
                                        wasColumnModified = v != (string)newValue;
                                        break;
                                    case Instant v:
                                        wasColumnModified = v != (Instant)newValue;
                                        break;
                                    case long v:
                                        wasColumnModified = v != (long)newValue;
                                        break;
                                    case double v:
                                        wasColumnModified = v != (double)newValue;
                                        break;
                                    case LatLng v:
                                        wasColumnModified = v.Latitude != ((LatLng)newValue).Latitude || v.Longitude != ((LatLng)newValue).Longitude;
                                        break;
                                    case Key v:
                                        wasColumnModified = !v.Equals((Key)newValue);
                                        break;
                                    default:
                                        // Don't know how to compare this type yet, assume modified.
                                        wasColumnModified = true;
                                        break;
                                }
                            }
                        }

                        if (wasColumnModified)
                        {
                            columns.Add($"KEYCOLUMN:{@namespace}:{entity.GetKind()}:{kv.Key}");
                        }
                    }
                }
                if (columns.Count > 0)
                {
                    using (_managedTracer.StartSpan("db.rediscache.cache.purge_columns"))
                    {
                        var queriesFlushed = await db.ScriptEvaluateAsync(_purgeColumns, columns.Select(x => new RedisKey(x)).ToArray()).ConfigureAwait(false);
                        if (metrics != null)
                        {
                            metrics.CacheQueriesFlushed += ((long)queriesFlushed);
                            await _metricService.AddPoint(_cacheInvalidations, ((long)queriesFlushed), null, new Dictionary<string, string?>
                            {
                                { "kind", "(transaction commit)" },
                            }).ConfigureAwait(false);
                        }
                    }
                }
            }
        }
    }
}
