namespace Redpoint.CloudFramework.Repository.Layers
{
    using Google.Cloud.Datastore.V1;
    using Grpc.Core;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Metric;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Expression;
    using Redpoint.CloudFramework.Repository.Converters.Model;
    using Redpoint.CloudFramework.Repository.Geographic;
    using Redpoint.CloudFramework.Repository.Hooks;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Tracing;
    using Redpoint.Collections;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DatastoreRepositoryLayer : IDatastoreRepositoryLayer
    {
        private readonly IModelConverter<Entity> _entityConverter;
        private readonly IHostEnvironment _hostEnvironment;
        private readonly IGoogleServices _googleServices;
        private readonly IManagedTracer _managedTracer;
        private readonly IGlobalRepositoryHook[] _hooks;
        private readonly IMemoryCache _memoryCache;
        private readonly IExpressionConverter _expressionConverter;
        private readonly ILogger<DatastoreRepositoryLayer> _logger;
        private readonly IMetricService _metricService;
        private readonly DatastoreClient _client;

        private readonly MemoryCacheEntryOptions _memoryCacheOptions =
            new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(30))
                .SetAbsoluteExpiration(TimeSpan.FromHours(12));

        private const string _datastoreEntityOperationCount = "rcf/datastore/operations";
        private const string _datastoreEntityEntityReadCount = "rcf/datastore/entity_reads";

        public DatastoreRepositoryLayer(
            IModelConverter<Entity> entityConverter,
            IHostEnvironment hostEnvironment,
            IGoogleServices googleServices,
            IManagedTracer managedTracer,
            IGlobalRepositoryHook[] hooks,
            IMemoryCache memoryCache,
            IExpressionConverter expressionConverter,
            ILogger<DatastoreRepositoryLayer> logger,
            IMetricService metricService)
        {
            _entityConverter = entityConverter;
            _hostEnvironment = hostEnvironment;
            _googleServices = googleServices;
            _managedTracer = managedTracer;
            _hooks = hooks;
            _memoryCache = memoryCache;
            _expressionConverter = expressionConverter;
            _logger = logger;
            _metricService = metricService;

            _client = _googleServices.Build<DatastoreClient, DatastoreClientBuilder>(
                DatastoreClient.DefaultEndpoint,
                DatastoreClient.DefaultScopes);
        }

        public AsyncEvent<EntitiesModifiedEventArgs> OnNonTransactionalEntitiesModified { get; } = new AsyncEvent<EntitiesModifiedEventArgs>();

        private DatastoreDb GetDbForNamespace(string @namespace)
        {
            using (_managedTracer.StartSpan($"db.datastore.get_datastore_for_current_site", @namespace))
            {
                var db = _memoryCache.Get<DatastoreDb>("db:" + @namespace);
                if (db != null)
                {
                    return db;
                }

                db = DatastoreDb.Create(_googleServices.ProjectId, @namespace, _client);
                _memoryCache.Set<DatastoreDb>("db:" + @namespace, db, _memoryCacheOptions);
                return db;
            }
        }

        private async IAsyncEnumerable<IReadOnlyList<T>> BatchedQueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (var span = _managedTracer.StartSpan("db.datastore.query", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                ArgumentNullException.ThrowIfNull(where);

                long totalEntitiesRead = 0;

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    GeoQueryParameters<T>? geoQuery = null;

                    var hasAncestorQuery = false;
                    var referenceModel = new T();
                    var filter = _expressionConverter.SimplifyFilter(_expressionConverter.ConvertExpressionToFilter(where.Body, where.Parameters[0], referenceModel, ref geoQuery, ref hasAncestorQuery));
                    var sort = order == null ? null : _expressionConverter.ConvertExpressionToOrder(order.Body, order.Parameters[0], referenceModel, ref geoQuery);

                    if (geoQuery == null)
                    {
                        var query = new Query(referenceModel.GetKind());
                        query.Filter = filter;
                        query.Limit = limit;
                        if (sort != null)
                        {
                            query.Order.AddRange(sort);
                        }

                        cancellationToken.ThrowIfCancellationRequested();

                        if (transaction != null && !hasAncestorQuery && (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsStaging()))
                        {
                            _logger.LogWarning("Detected a transactional query without an ancestor filter in development. The datastore emulator does not support ancestor-less transactional queries, even though this is supported in production. The query will be non-transactional in development, but transactionality and correctness will be enforced in production.");
                            transaction = null;
                        }

                        var db = GetDbForNamespace(@namespace);
                        AsyncLazyDatastoreQuery lazyQuery;
                        if (transaction == null)
                        {
                            lazyQuery = db.RunQueryLazilyAsync(query);
                        }
                        else
                        {
                            lazyQuery = transaction.Transaction.RunQueryLazilyAsync(query);
                        }

                        int entitiesRead = 0;
                        await foreach (var response in lazyQuery.AsResponses().WithCancellation(cancellationToken))
                        {
                            entitiesRead += response.Batch.EntityResults.Count;
                            if (metrics != null)
                            {
                                metrics.DatastoreEntitiesRead += response.Batch.EntityResults.Count;
                            }
                            cancellationToken.ThrowIfCancellationRequested();

                            yield return
                                response.Batch.EntityResults
                                .Select(x => _entityConverter.From<T>(@namespace, x.Entity))
                                .WhereNotNull()
                                .ToList();
                        }

                        await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                        {
                            { "operation", "query" },
                            { "kind", referenceModel.GetKind() },
                            { "namespace", @namespace },
                        }).ConfigureAwait(false);
                        await _metricService.AddPoint(_datastoreEntityEntityReadCount, entitiesRead, null, new Dictionary<string, string?>
                        {
                            { "kind", referenceModel.GetKind() },
                            { "namespace", @namespace },
                        }).ConfigureAwait(false);
                        totalEntitiesRead += entitiesRead;
                    }
                    else
                    {
                        var keyLength = ((IGeoModel)referenceModel).GetHashKeyLengthsForGeopointFields()[geoQuery.GeoFieldName];
                        var latLngRect = S2Manager.LatLngRectFromQueryRectangleInput(geoQuery.MinPoint, geoQuery.MaxPoint);
                        var ranges = S2Manager.GetGeohashRanges(latLngRect, keyLength);

                        var entityBatches = ranges.ToAsyncEnumerable()
                            .SelectMany(range => QueryGeohashRange(
                                @namespace,
                                referenceModel,
                                filter!, // @note: If filter is null, then we won't be doing a geographic query anyway.
                                range,
                                keyLength,
                                geoQuery,
                                transaction,
                                metrics,
                                cancellationToken));

                        if (geoQuery.SortDirection.HasValue)
                        {
                            // @note: We return these all as one giant batch; we can't propagate the Datastore
                            // batching to the receiver due to the sorting.
                            var entities = entityBatches.SelectMany(x => x.ToAsyncEnumerable());
                            if (geoQuery.SortDirection.Value == PropertyOrder.Types.Direction.Ascending)
                            {
                                entities = entities.OrderBy(x => GeoExtensions.HaversineDistance(geoQuery.CenterPoint, geoQuery.ServerSideAccessor(x)));
                            }
                            else
                            {
                                entities = entities.OrderByDescending(x => GeoExtensions.HaversineDistance(geoQuery.CenterPoint, geoQuery.ServerSideAccessor(x)));
                            }
                            if (limit != null)
                            {
                                entities = entities.Take(limit.Value);
                            }
                            yield return await entities.ToListAsync(cancellationToken).ConfigureAwait(false);
                        }
                        else
                        {
                            // Only pull as many batches as we need to satisfy the query.
                            var emittedEntityCount = 0;
                            await foreach (var batch in entityBatches.ConfigureAwait(false))
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                var nextEntityCount = emittedEntityCount + batch.Count;
                                if (limit != null && nextEntityCount > limit.Value)
                                {
                                    yield return batch.Take(nextEntityCount - emittedEntityCount).ToList();
                                    yield break;
                                }
                                else
                                {
                                    yield return batch;
                                }
                                emittedEntityCount += batch.Count;
                            }
                        }
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
                    }
                }

                if (totalEntitiesRead > 2500)
                {
                    _logger.LogWarning($"QueryAsync operation returned more than 2500 '{new T().GetKind()}' entities, which is likely to cause high Datastore costs. Please optimize your application.");
                }

                span.SetTag("TotalEntitiesRead", totalEntitiesRead.ToString(CultureInfo.InvariantCulture));
                span.SetExtra("TotalEntitiesRead", totalEntitiesRead);
            }
        }

        public IBatchedAsyncEnumerable<T> QueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new() =>
            BatchedQueryAsync(
                @namespace,
                where,
                order,
                limit,
                transaction,
                metrics,
                cancellationToken).AsBatchedAsyncEnumerable();

        private async IAsyncEnumerable<IReadOnlyList<T>> QueryGeohashRange<T>(
            string @namespace,
            T referenceModel,
            Filter filter,
            S2Manager.GeohashRange range,
            ushort keyLength,
            GeoQueryParameters<T> geoQuery,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.datastore.query_geohash_range", $"{@namespace},{typeof(T).Name}"))
            {
                var hashKey = S2Manager.GenerateGeohashKey(range.RangeMin, keyLength);
                var hashKeyString = hashKey.ToString(CultureInfo.InvariantCulture);

                var filtersGeographic = Filter.And(
                    Filter.GreaterThan(geoQuery.GeoFieldName + GeoConstants.GeoHashPropertySuffix, new Value { StringValue = range.RangeMin.ToString(CultureInfo.InvariantCulture) }),
                    Filter.LessThan(geoQuery.GeoFieldName + GeoConstants.GeoHashPropertySuffix, new Value { StringValue = range.RangeMax.ToString(CultureInfo.InvariantCulture) }),
                    Filter.Equal(geoQuery.GeoFieldName + GeoConstants.HashKeyPropertySuffix, new Value { IntegerValue = (long)hashKey })
                );

                var query = new Query(referenceModel.GetKind());
                if (filter == null)
                {
                    query.Filter = filtersGeographic;
                }
                else
                {
                    query.Filter = _expressionConverter.SimplifyFilter(Filter.And(filter, filtersGeographic));
                }

                cancellationToken.ThrowIfCancellationRequested();

                var db = GetDbForNamespace(@namespace);
                AsyncLazyDatastoreQuery lazyQuery;
                if (transaction == null)
                {
                    lazyQuery = db.RunQueryLazilyAsync(query);
                }
                else
                {
                    lazyQuery = transaction.Transaction.RunQueryLazilyAsync(query);
                }

                int entitiesRead = 0;
                await foreach (var response in lazyQuery.AsResponses().WithCancellation(cancellationToken))
                {
                    entitiesRead += response.Batch.EntityResults.Count;
                    if (metrics != null)
                    {
                        metrics.DatastoreEntitiesRead += response.Batch.EntityResults.Count;
                    }
                    cancellationToken.ThrowIfCancellationRequested();

                    yield return
                        response.Batch.EntityResults
                        .Select(x => _entityConverter.From<T>(@namespace, x.Entity))
                        .WhereNotNull()
                        .Where(geoQuery.ServerSideFilter)
                        .ToList();
                }

                await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                    {
                        { "operation", "querygeo" },
                        { "hashkey", hashKeyString },
                        { "kind", referenceModel.GetKind() },
                        { "namespace", @namespace },
                    }).ConfigureAwait(false);
                await _metricService.AddPoint(_datastoreEntityEntityReadCount, entitiesRead, null, new Dictionary<string, string?>
                    {
                        { "kind", referenceModel.GetKind() },
                        { "namespace", @namespace },
                    }).ConfigureAwait(false);
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
            using (_managedTracer.StartSpan($"db.datastore.query_paginated", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                ArgumentNullException.ThrowIfNull(where);

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    GeoQueryParameters<T>? geoQuery = null;

                    var hasAncestorQuery = false;
                    var referenceModel = new T();
                    var filter = _expressionConverter.SimplifyFilter(_expressionConverter.ConvertExpressionToFilter(where.Body, where.Parameters[0], referenceModel, ref geoQuery, ref hasAncestorQuery));
                    if (geoQuery != null)
                    {
                        throw new InvalidOperationException("Geographic queries can not be used with QueryPaginatedAsync, because there is no way to paginate geographic queries. Use QueryAsync<> instead.");
                    }
                    var sort = order == null ? null : _expressionConverter.ConvertExpressionToOrder(order.Body, order.Parameters[0], referenceModel, ref geoQuery);

                    var query = new Query(referenceModel.GetKind());
                    query.Filter = filter;
                    query.Limit = limit;
                    query.StartCursor = cursor;
                    if (sort != null)
                    {
                        query.Order.AddRange(sort);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    var db = GetDbForNamespace(@namespace);
                    DatastoreQueryResults results;
                    if (transaction == null)
                    {
                        results = await db.RunQueryAsync(query).ConfigureAwait(false);
                    }
                    else
                    {
                        results = await transaction.Transaction.RunQueryAsync(query).ConfigureAwait(false);
                    }

                    await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                    {
                        { "operation", "querypage" },
                        { "kind", referenceModel.GetKind() },
                        { "namespace", @namespace },
                    }).ConfigureAwait(false);
                    await _metricService.AddPoint(_datastoreEntityEntityReadCount, results.Entities.Count, null, new Dictionary<string, string?>
                    {
                        { "kind", referenceModel.GetKind() },
                        { "namespace", @namespace },
                    }).ConfigureAwait(false);

                    // The Datastore emulator has a bug where it will always return MoreResultsAfterLimit
                    // even when the paginated query is complete. Handle this scenario with the emulator.
                    if ((_hostEnvironment.IsDevelopment() || _hostEnvironment.IsStaging()) &&
                        results.Entities.Count < limit &&
                        results.MoreResults == QueryResultBatch.Types.MoreResultsType.MoreResultsAfterLimit)
                    {
                        return new PaginatedQueryResult<T>
                        {
                            Results = results.Entities.Select(x => _entityConverter.From<T>(@namespace, x)).WhereNotNull().ToList(),
                            NextCursor = null,
                        };
                    }

                    return new PaginatedQueryResult<T>
                    {
                        Results = results.Entities.Select(x => _entityConverter.From<T>(@namespace, x)).WhereNotNull().ToList(),
                        NextCursor = results.MoreResults == QueryResultBatch.Types.MoreResultsType.MoreResultsAfterLimit ? new PaginatedQueryCursor(results.EndCursor) : null,
                    };
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
                    }
                }
            }
        }

        public async Task<T?> LoadAsync<T>(
            string @namespace,
            Key key,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.datastore.load", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                ArgumentNullException.ThrowIfNull(key);

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    var db = GetDbForNamespace(@namespace);

                    Entity entity;
                    if (transaction == null)
                    {
                        entity = await db.LookupAsync(key).ConfigureAwait(false);
                    }
                    else
                    {
                        entity = await transaction.Transaction.LookupAsync(key).ConfigureAwait(false);
                    }

                    if (metrics != null)
                    {
                        metrics.DatastoreEntitiesRead++;
                    }

                    await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                    {
                        { "operation", "load" },
                        { "kind", key.Path.Last().Kind },
                        { "namespace", @namespace },
                    }).ConfigureAwait(false);
                    if (entity != null)
                    {
                        await _metricService.AddPoint(_datastoreEntityEntityReadCount, 1, null, new Dictionary<string, string?>
                        {
                            { "kind", key.Path.Last().Kind },
                            { "namespace", @namespace },
                        }).ConfigureAwait(false);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    return entity == null ? null : _entityConverter.From<T>(@namespace, entity);
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
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
            using (_managedTracer.StartSpan($"db.datastore.load", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                ArgumentNullException.ThrowIfNull(keys);

                long totalEntitiesRead = 0;

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                string? kind = null;
                try
                {
                    var db = GetDbForNamespace(@namespace);

                    var batch = new HashSet<Key>();
                    var hasWrittenOpMetric = false;
                    await foreach (var key in keys.Distinct().WithCancellation(cancellationToken))
                    {
                        if (key == null)
                        {
                            throw new ArgumentNullException(nameof(keys), "One or more keys passed to LoadAsync was null.");
                        }

                        batch.Add(key);

                        if (kind == null)
                        {
                            kind = key.Path.Last().Kind;
                            if (!hasWrittenOpMetric && kind != null)
                            {
                                await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                                {
                                    { "operation", "load" },
                                    { "kind", kind },
                                    { "namespace", @namespace },
                                }).ConfigureAwait(false);
                            }
                        }

                        if (batch.Count == 1000)
                        {
                            IReadOnlyList<Entity> entities;
                            if (transaction == null)
                            {
                                entities = await db.LookupAsync(batch).ConfigureAwait(false);
                            }
                            else
                            {
                                entities = await transaction.Transaction.LookupAsync(batch).ConfigureAwait(false);
                            }

                            if (metrics != null)
                            {
                                metrics.DatastoreEntitiesRead += batch.Count;
                            }

                            totalEntitiesRead += entities.Count;
                            await _metricService.AddPoint(_datastoreEntityEntityReadCount, entities.Count, null, new Dictionary<string, string?>
                                {
                                    { "kind", kind },
                                    { "namespace", @namespace },
                                }).ConfigureAwait(false);

                            var expectedKeys = new List<Key>(batch);
                            batch.Clear();

                            cancellationToken.ThrowIfCancellationRequested();

                            var batchResults = new List<KeyValuePair<Key, T?>>();
                            for (int i = 0; i < expectedKeys.Count; i++)
                            {
                                if (entities.Count <= i ||
                                entities[i] == null)
                                {
                                    batchResults.Add(new KeyValuePair<Key, T?>(expectedKeys[i], null));
                                }
                                else
                                {
                                    batchResults.Add(new KeyValuePair<Key, T?>(entities[i].Key, _entityConverter.From<T>(@namespace, entities[i])));
                                }
                            }
                            yield return batchResults;
                        }
                    }

                    if (!hasWrittenOpMetric && kind != null)
                    {
                        await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                        {
                            { "operation", "load" },
                            { "kind", kind },
                            { "namespace", @namespace },
                        }).ConfigureAwait(false);
                    }

                    if (batch.Count > 0)
                    {
                        IReadOnlyList<Entity> entities;
                        if (transaction == null)
                        {
                            entities = await db.LookupAsync(batch).ConfigureAwait(false);
                        }
                        else
                        {
                            entities = await transaction.Transaction.LookupAsync(batch).ConfigureAwait(false);
                        }

                        if (metrics != null)
                        {
                            metrics.DatastoreEntitiesRead += batch.Count;
                        }

                        totalEntitiesRead += entities.Count;
                        await _metricService.AddPoint(_datastoreEntityEntityReadCount, entities.Count, null, new Dictionary<string, string?>
                        {
                            { "kind", kind },
                            { "namespace", @namespace },
                        }).ConfigureAwait(false);

                        var expectedKeys = new List<Key>(batch);
                        batch.Clear();

                        cancellationToken.ThrowIfCancellationRequested();

                        var batchResults = new List<KeyValuePair<Key, T?>>();
                        for (int i = 0; i < expectedKeys.Count; i++)
                        {
                            if (entities.Count <= i ||
                            entities[i] == null)
                            {
                                batchResults.Add(new KeyValuePair<Key, T?>(expectedKeys[i], null));
                            }
                            else
                            {
                                batchResults.Add(new KeyValuePair<Key, T?>(entities[i].Key, _entityConverter.From<T>(@namespace, entities[i])));
                            }
                        }
                        yield return batchResults;
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
                    }
                }

                if (totalEntitiesRead > 2500)
                {
                    _logger.LogWarning($"LoadAsync operation returned more than 2500 '{kind}' entities, which is likely to cause high Datastore costs. Please optimize your application.");
                }
            }
        }

        public async IAsyncEnumerable<KeyValuePair<Key, T?>> LoadAcrossNamespacesAsync<T>(
            IAsyncEnumerable<Key> keys,
            RepositoryOperationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.datastore.load_across_namespaces", $"{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(keys);

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    // We don't currently process keys asynchronously; we eagerly fetch them all so we can do
                    // our Any/GroupBy operations.
                    var keysList = await keys.ToListAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                    var kind = keysList.FirstOrDefault()?.Path?.Last()?.Kind;

                    if (keysList.Any(x => x == null))
                    {
                        throw new ArgumentNullException(nameof(keys), "One or more keys passed to LoadAcrossNamespacesAsync was null.");
                    }

                    foreach (var keyGroup in keysList.GroupBy(x => x.PartitionId.NamespaceId).ToDictionary(k => k.Key, v => v.ToArray()))
                    {
                        var @namespace = keyGroup.Key;
                        // Datastore APIs enforce that the namespace can't be null, so we don't need to check and throw ArgumentNullException.

                        var batches = new List<Key[]>();
                        if (keyGroup.Value.Length <= 1000)
                        {
                            batches.Add(keyGroup.Value);
                        }
                        else
                        {
                            for (int i = 0; i < keyGroup.Value.Length; i += 1000)
                            {
                                var batchSize = Math.Min(1000, keyGroup.Value.Length - i);
                                var batch = new Key[batchSize];
                                Array.Copy(keyGroup.Value, i, batch, 0, batchSize);
                                batches.Add(batch);
                            }
                        }

                        var db = GetDbForNamespace(@namespace);

                        await _metricService.AddPoint(_datastoreEntityOperationCount, 1, null, new Dictionary<string, string?>
                        {
                            { "operation", "loadacrossns" },
                            { "kind", kind },
                            { "namespace", @namespace },
                        }).ConfigureAwait(false);

                        foreach (var batch in batches)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            var entities = await db.LookupAsync(batch).ConfigureAwait(false);

                            if (metrics != null)
                            {
                                metrics.DatastoreEntitiesRead += batch.LongLength;
                            }

                            await _metricService.AddPoint(_datastoreEntityEntityReadCount, entities.Count, null, new Dictionary<string, string?>
                            {
                                { "kind", kind },
                                { "namespace", @namespace },
                            }).ConfigureAwait(false);

                            cancellationToken.ThrowIfCancellationRequested();

                            for (int i = 0; i < batch.Length; i++)
                            {
                                if (entities.Count <= i ||
                                entities[i] == null)
                                {
                                    yield return new KeyValuePair<Key, T?>(batch[i], null);
                                }
                                else
                                {
                                    yield return new KeyValuePair<Key, T?>(entities[i].Key, _entityConverter.From<T>(@namespace, entities[i]));
                                }
                            }
                        }
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
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
            using (_managedTracer.StartSpan($"db.datastore.create", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    var db = GetDbForNamespace(@namespace);

                    List<Entity> entities = new List<Entity>();
                    List<T> modelBuffer = new List<T>();
                    KeyFactory? keyFactory = null;
                    await foreach (var model in models.ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (model == null)
                        {
                            throw new ArgumentNullException(nameof(models), "Input models contained a null value; filter nulls out of the input enumerable before calling CreateAsync().");
                        }

                        if (keyFactory == null)
                        {
                            keyFactory = db.CreateKeyFactory(model.GetKind());
                        }

                        var entity = _entityConverter.To(@namespace, model, true, _ => keyFactory.CreateIncompleteKey());
                        if (entity.Key.PartitionId.NamespaceId != @namespace)
                        {
                            throw new InvalidOperationException($"Cross-namespace data write attempted (CreateAsync called with namespace '{@namespace}', but entity had namespace '{entity.Key.PartitionId.NamespaceId}').");
                        }
                        entities.Add(entity);
                        modelBuffer.Add(model);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (transaction == null)
                    {
                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        var keys = new List<Key>();
                        foreach (var batch in entities.BatchInto(500))
                        {
                            keys.AddRange(await db.InsertAsync(batch).ConfigureAwait(false));
                        }
                        if (metrics != null)
                        {
                            metrics.DatastoreEntitiesWritten += entities.Count;
                        }

                        for (int i = 0; i < keys.Count; i++)
                        {
                            if (keys[i] != null)
                            {
                                modelBuffer[i].Key = keys[i];
                            }
                        }

                        foreach (var hook in _hooks)
                        {
                            foreach (var model in modelBuffer)
                            {
                                await hook.PostCreate(@namespace, model, transaction).ConfigureAwait(false);
                            }
                        }

                        await OnNonTransactionalEntitiesModified.BroadcastAsync(new EntitiesModifiedEventArgs
                        {
                            Keys = modelBuffer.Select(x => x.Key).ToArray(),
                            Metrics = metrics,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (var entity in entities)
                        {
                            if (entity.Key.Path.Last().IdTypeCase == Key.Types.PathElement.IdTypeOneofCase.None)
                            {
                                entity.Key = await db.AllocateIdAsync(entity.Key).ConfigureAwait(false);
                            }
                        }

                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        foreach (var batch in entities.BatchInto(500))
                        {
                            transaction.Transaction.Insert(batch);
                        }
                        transaction.ModifiedModelsList.AddRange(modelBuffer);
                        transaction.QueuedPreCommitOperationsList.Add(async () =>
                        {
                            foreach (var hook in _hooks)
                            {
                                foreach (var model in modelBuffer)
                                {
                                    await hook.PostCreate(@namespace, model, transaction).ConfigureAwait(false);
                                }
                            }
                        });

                        for (int i = 0; i < entities.Count; i++)
                        {
                            modelBuffer[i].Key = entities[i].Key;
                        }
                    }

                    foreach (var model in modelBuffer)
                    {
                        yield return model;
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
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
            using (_managedTracer.StartSpan($"db.datastore.upsert", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    var db = GetDbForNamespace(@namespace);

                    List<Entity> entities = new List<Entity>();
                    List<T> modelBuffer = new List<T>();
                    HashSet<Key> seenKeys = new HashSet<Key>();
                    KeyFactory? keyFactory = null;
                    await foreach (var model in models.ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (model == null)
                        {
                            throw new ArgumentNullException(nameof(models), "Input models contained a null value; filter nulls out of the input enumerable before calling UpsertAsync().");
                        }

                        if (keyFactory == null)
                        {
                            keyFactory = db.CreateKeyFactory(model.GetKind());
                        }

                        var entity = _entityConverter.To(@namespace, model, false, _ => keyFactory.CreateIncompleteKey());
                        if (entity.Key.PartitionId.NamespaceId != @namespace)
                        {
                            throw new InvalidOperationException($"Cross-namespace data write attempted (UpsertAsync called with namespace '{@namespace}', but entity had namespace '{entity.Key.PartitionId.NamespaceId}').");
                        }

                        if (model.Key != null && seenKeys.Contains(entity.Key))
                        {
                            continue;
                        }

                        entities.Add(entity);
                        modelBuffer.Add(model);
                        seenKeys.Add(entity.Key);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (transaction == null)
                    {
                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        var keys = new List<Key>();
                        foreach (var batch in entities.BatchInto(500))
                        {
                            keys.AddRange(await db.UpsertAsync(batch).ConfigureAwait(false));
                        }
                        if (metrics != null)
                        {
                            metrics.DatastoreEntitiesWritten += entities.Count;
                        }

                        for (int i = 0; i < keys.Count; i++)
                        {
                            if (keys[i] != null)
                            {
                                modelBuffer[i].Key = keys[i];
                            }
                        }

                        foreach (var hook in _hooks)
                        {
                            foreach (var model in modelBuffer)
                            {
                                await hook.PostUpsert(@namespace, model, transaction).ConfigureAwait(false);
                            }
                        }

                        await OnNonTransactionalEntitiesModified.BroadcastAsync(new EntitiesModifiedEventArgs
                        {
                            Keys = modelBuffer.Select(x => x.Key).ToArray(),
                            Metrics = metrics,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (var entity in entities)
                        {
                            if (entity.Key.Path.Last().IdTypeCase == Key.Types.PathElement.IdTypeOneofCase.None)
                            {
                                entity.Key = await db.AllocateIdAsync(entity.Key).ConfigureAwait(false);
                            }
                        }

                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        foreach (var batch in entities.BatchInto(500))
                        {
                            transaction.Transaction.Upsert(batch);
                        }
                        transaction.ModifiedModelsList.AddRange(modelBuffer);
                        transaction.QueuedPreCommitOperationsList.Add(async () =>
                        {
                            foreach (var hook in _hooks)
                            {
                                foreach (var model in modelBuffer)
                                {
                                    await hook.PostUpsert(@namespace, model, transaction).ConfigureAwait(false);
                                }
                            }
                        });

                        for (int i = 0; i < entities.Count; i++)
                        {
                            modelBuffer[i].Key = entities[i].Key;
                        }
                    }

                    foreach (var model in modelBuffer)
                    {
                        yield return model;
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
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
            using (_managedTracer.StartSpan($"db.datastore.update", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    var db = GetDbForNamespace(@namespace);

                    List<Entity> entities = new List<Entity>();
                    List<T> modelBuffer = new List<T>();
                    HashSet<Key> seenKeys = new HashSet<Key>();
                    KeyFactory? keyFactory = null;
                    await foreach (var model in models.ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (model == null || model.Key == null)
                        {
                            throw new ArgumentNullException(nameof(models), "Input models contained a null value or had a null Key on the model; filter nulls out of the input enumerable before calling UpdateAsync().");
                        }

                        if (keyFactory == null)
                        {
                            keyFactory = db.CreateKeyFactory(model.GetKind());
                        }

                        var entity = _entityConverter.To(@namespace, model, false, _ => keyFactory.CreateIncompleteKey());
                        if (entity.Key.PartitionId.NamespaceId != @namespace)
                        {
                            throw new InvalidOperationException($"Cross-namespace data write attempted (UpdateAsync called with namespace '{@namespace}', but entity had namespace '{entity.Key.PartitionId.NamespaceId}').");
                        }

                        if (model.Key != null && seenKeys.Contains(entity.Key))
                        {
                            continue;
                        }

                        entities.Add(entity);
                        modelBuffer.Add(model);
                        seenKeys.Add(entity.Key);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (transaction == null)
                    {
                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        foreach (var batch in entities.BatchInto(500))
                        {
                            await db.UpdateAsync(batch).ConfigureAwait(false);
                        }

                        if (metrics != null)
                        {
                            metrics.DatastoreEntitiesWritten += entities.Count;
                        }

                        foreach (var hook in _hooks)
                        {
                            foreach (var model in modelBuffer)
                            {
                                await hook.PostUpdate(@namespace, model, transaction).ConfigureAwait(false);
                            }
                        }

                        await OnNonTransactionalEntitiesModified.BroadcastAsync(new EntitiesModifiedEventArgs
                        {
                            Keys = modelBuffer.Select(x => x.Key).ToArray(),
                            Metrics = metrics,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        foreach (var batch in entities.BatchInto(500))
                        {
                            transaction.Transaction.Update(batch);
                        }
                        transaction.ModifiedModelsList.AddRange(modelBuffer);
                        transaction.QueuedPreCommitOperationsList.Add(async () =>
                        {
                            foreach (var hook in _hooks)
                            {
                                foreach (var model in modelBuffer)
                                {
                                    await hook.PostUpdate(@namespace, model, transaction).ConfigureAwait(false);
                                }
                            }
                        });
                    }

                    foreach (var model in modelBuffer)
                    {
                        yield return model;
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
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
            using (_managedTracer.StartSpan($"db.datastore.delete", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    var db = GetDbForNamespace(@namespace);

                    List<Entity> entities = new List<Entity>();
                    List<T> modelBuffer = new List<T>();
                    HashSet<Key> seenKeys = new HashSet<Key>();
                    KeyFactory? keyFactory = null;
                    await foreach (var model in models.ConfigureAwait(false))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (model == null || model.Key == null)
                        {
                            throw new ArgumentNullException(nameof(models), "Input models contained a null value or had a null Key on the model; filter nulls out of the input enumerable before calling DeleteAsync().");
                        }

                        if (keyFactory == null)
                        {
                            keyFactory = db.CreateKeyFactory(model.GetKind());
                        }

                        var entity = _entityConverter.To(@namespace, model, false, _ => keyFactory.CreateIncompleteKey());
                        if (entity.Key.PartitionId.NamespaceId != @namespace)
                        {
                            throw new InvalidOperationException($"Cross-namespace data write attempted (DeleteAsync called with namespace '{@namespace}', but entity had namespace '{entity.Key.PartitionId.NamespaceId}').");
                        }

                        if (model.Key != null && seenKeys.Contains(entity.Key))
                        {
                            continue;
                        }

                        entities.Add(entity);
                        modelBuffer.Add(model);
                        seenKeys.Add(entity.Key);
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (transaction == null)
                    {
                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        foreach (var batch in entities.BatchInto(500))
                        {
                            await db.DeleteAsync(batch).ConfigureAwait(false);
                        }
                        if (metrics != null)
                        {
                            metrics.DatastoreEntitiesDeleted += entities.Count;
                        }

                        foreach (var hook in _hooks)
                        {
                            foreach (var model in modelBuffer)
                            {
                                await hook.PostDelete(@namespace, model, transaction).ConfigureAwait(false);
                            }
                        }

                        await OnNonTransactionalEntitiesModified.BroadcastAsync(new EntitiesModifiedEventArgs
                        {
                            Keys = modelBuffer.Select(x => x.Key).ToArray(),
                            Metrics = metrics,
                        }, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        foreach (var hook in _hooks)
                        {
                            foreach (var entity in entities)
                            {
                                await hook.MutateEntityBeforeWrite(@namespace, entity).ConfigureAwait(false);
                            }
                        }

                        foreach (var batch in entities.BatchInto(500))
                        {
                            transaction.Transaction.Delete(batch);
                        }
                        transaction.ModifiedModelsList.AddRange(modelBuffer);
                        transaction.QueuedPreCommitOperationsList.Add(async () =>
                        {
                            foreach (var hook in _hooks)
                            {
                                foreach (var model in modelBuffer)
                                {
                                    await hook.PostDelete(@namespace, model, transaction).ConfigureAwait(false);
                                }
                            }
                        });
                    }
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
                    }
                }
            }
        }

        public Task<Key> AllocateKeyAsync<T>(
            string @namespace,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.datastore.allocate_key", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                var referenceModel = new T();
                var db = GetDbForNamespace(@namespace);
                var factory = db.CreateKeyFactory(referenceModel.GetKind());
                var key = factory.CreateIncompleteKey();
                return db.AllocateIdAsync(key);
            }
        }

        public Task<KeyFactory> GetKeyFactoryAsync<T>(
            string @namespace,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new()
        {
            using (_managedTracer.StartSpan($"db.datastore.get_key_factory", $"{@namespace},{typeof(T).Name}"))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                var referenceModel = new T();
                var db = GetDbForNamespace(@namespace);
                return Task.FromResult(db.CreateKeyFactory(referenceModel.GetKind()));
            }
        }

        public async Task<IModelTransaction> BeginTransactionAsync(
            string @namespace,
            TransactionMode mode,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            using (_managedTracer.StartSpan($"db.datastore.begin_transaction", @namespace))
            {
                ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));

                return new TopLevelModelTransaction(
                    @namespace,
                    await GetDbForNamespace(@namespace)
                        .BeginTransactionAsync(
                            mode == TransactionMode.ReadOnly
                            ? TransactionOptions.CreateReadOnly()
                            : TransactionOptions.CreateReadWrite())
                        .ConfigureAwait(false),
                    this);
            }
        }

        public async Task RollbackAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));
            ArgumentNullException.ThrowIfNull(transaction);

            if (transaction.IsNestedTransaction)
            {
                throw new ArgumentException("You can not called RollbackAsync on a nested transaction; check IsNestedTransaction before calling RollbackAsync!", nameof(transaction));
            }

            using (_managedTracer.StartSpan($"db.datastore.rollback", @namespace))
            {
                if (transaction.HasCommitted)
                {
                    throw new ArgumentException("This transaction has already been committed!", nameof(transaction));
                }
                if (transaction.HasRolledBack)
                {
                    throw new ArgumentException("This transaction has already been rolled back!", nameof(transaction));
                }

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    try
                    {
                        await transaction.Transaction.RollbackAsync().ConfigureAwait(false);
                    }
                    catch (RpcException ex) when (ex.IsTransactionExpiryException())
                    {
                        // Rollback isn't needed, continue.
                    }

                    transaction.HasRolledBack = true;
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
                    }
                }
            }
        }

        public async Task CommitAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(@namespace, nameof(@namespace));
            ArgumentNullException.ThrowIfNull(transaction);

            if (transaction.IsNestedTransaction)
            {
                throw new ArgumentException("You can not called CommitAsync on a nested transaction; check IsNestedTransaction before calling CommitAsync!", nameof(transaction));
            }

            using (_managedTracer.StartSpan($"db.datastore.commit", @namespace))
            {
                if (transaction.HasCommitted)
                {
                    throw new ArgumentException("This transaction has already been committed!", nameof(transaction));
                }
                if (transaction.HasRolledBack)
                {
                    throw new ArgumentException("This transaction has already been rolled back!", nameof(transaction));
                }

                Stopwatch? stopwatch = null;
                if (metrics != null)
                {
                    stopwatch = Stopwatch.StartNew();
                }
                try
                {
                    foreach (var action in transaction.QueuedPreCommitOperations)
                    {
                        await action().ConfigureAwait(false);
                    }

                    await transaction.Transaction.CommitAsync().ConfigureAwait(false);

                    transaction.HasCommitted = true;

                    // TODO: Figure out the number of entities written/deleted and write them into the metrics.
                }
                finally
                {
                    if (metrics != null)
                    {
                        metrics.DatastoreElapsedMilliseconds = stopwatch!.ElapsedMilliseconds;
                    }
                }
            }
        }
    }
}
