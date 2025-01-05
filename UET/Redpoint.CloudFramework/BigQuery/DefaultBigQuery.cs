namespace Redpoint.CloudFramework.BigQuery
{
    using Google;
    using Google.Apis.Bigquery.v2;
    using Google.Apis.Bigquery.v2.Data;
    using Google.Cloud.BigQuery.V2;
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.Caching.Memory;
    using Redpoint.CloudFramework.GoogleInfrastructure;
    using Redpoint.CloudFramework.Prefix;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    public class DefaultBigQuery : IBigQuery
    {
        private readonly IMemoryCache _memoryCache;
        private readonly IGlobalPrefix _globalPrefix;
        private readonly BigQueryClient _client;
        private readonly Dictionary<string, TableSchema> _knownSchemata;
        private readonly Dictionary<string, bool> _schemataHasExpiry;
        private static readonly object _bigQuerySchemaLock = new object();

        private MemoryCacheEntryOptions _memoryCacheOptions =
            new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(TimeSpan.FromSeconds(30));

        public DefaultBigQuery(
            IMemoryCache memoryCache,
            IGlobalPrefix globalPrefix,
            IGoogleServices googleServices)
        {
            _memoryCache = memoryCache;
            _globalPrefix = globalPrefix;

            ArgumentNullException.ThrowIfNull(googleServices);

            _client = googleServices.BuildRest<BigQueryClient, BigQueryClientBuilder>(
                new[]
                    {
                        BigqueryService.Scope.Bigquery,
                        BigqueryService.Scope.BigqueryInsertdata,
                        BigqueryService.Scope.DevstorageFullControl,
                        BigqueryService.Scope.CloudPlatform
                    });
            _knownSchemata = new Dictionary<string, TableSchema>();
            _schemataHasExpiry = new Dictionary<string, bool>();
        }

        public Key PublicDatasetKey
        {
            get
            {
                var k = new Key();
                k.Path.Add(new Key.Types.PathElement("PublicDataset", "public"));
                return k;
            }
        }

        public BigQueryClient GetBigQueryClient()
        {
            return _client;
        }

        public void DeclareSchemaForTable(string table, int tableVersion, bool hasAutomaticExpiration, TableSchema schema)
        {
            lock (_bigQuerySchemaLock)
            {
                _knownSchemata[table + "_v" + tableVersion] = schema;
                _schemataHasExpiry[table + "_v" + tableVersion] = hasAutomaticExpiration;
            }
        }

        public string GetDatasetNameForProject(Key projectKey)
        {
            ArgumentNullException.ThrowIfNull(projectKey);

            if (projectKey.Equals(PublicDatasetKey))
            {
                return "public";
            }

            return _globalPrefix.Create(projectKey).Replace('-', '_');
        }

        public string GetTableNameFromTableAndVersion(string table, int tableVersion)
        {
            return table + "_v" + tableVersion;
        }

        public async Task<BigQueryTable> GetWritableTableForProject(Key projectKey, string table, int tableVersion)
        {
            ArgumentNullException.ThrowIfNull(projectKey);

            if (projectKey.Equals(PublicDatasetKey))
            {
                return await GetWritableTableForProject("public", table, tableVersion).ConfigureAwait(false);
            }

            return await GetWritableTableForProject(_globalPrefix.Create(projectKey), table, tableVersion).ConfigureAwait(false);
        }

        public async Task<BigQueryTable> GetWritableTableForProject(string projectId, string table, int tableVersion)
        {
            ArgumentException.ThrowIfNullOrEmpty(projectId);

            var datasetName = projectId.Replace('-', '_');
            var tableName = table + "_v" + tableVersion;
            var cacheName = datasetName + "." + tableName;
            if (!_memoryCache.TryGetValue(cacheName, out BigQueryTable? tableRef))
            {
                var dataset = await _client.GetOrCreateDatasetAsync(datasetName).ConfigureAwait(false);
                var schema = _knownSchemata[tableName];
                try
                {
                    var tableToCreate = new Table
                    {
                        TimePartitioning = TimePartition.CreateDailyPartitioning(_schemataHasExpiry[tableName] ? TimeSpan.FromDays(30) : (TimeSpan?)null),
                        Schema = schema
                    };
                    tableRef = await _client.GetOrCreateTableAsync(
                        datasetName,
                        tableName,
                        tableToCreate).ConfigureAwait(false);
                }
                catch (GoogleApiException gae) when (gae.Message.Contains("Already Exists: Table", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Race condition with another thread creating the exact same table - retry with a get request.
                    tableRef = await _client.GetTableAsync(
                        datasetName,
                        tableName).ConfigureAwait(false);
                }

                tableRef = await CheckTableRefMatchesSchemaAndUpdateIfNecessary(datasetName, tableName, tableRef, schema).ConfigureAwait(false);

                _memoryCache.Set(cacheName, tableRef, _memoryCacheOptions);
            }

            return tableRef!;
        }

        private async Task<BigQueryTable> CheckTableRefMatchesSchemaAndUpdateIfNecessary(string datasetName, string tableName, BigQueryTable tableRef, TableSchema schema)
        {
            if (!FieldListIsEqual(tableRef.Schema.Fields, schema.Fields))
            {
                tableRef.Resource.Schema = schema;

                return await _client.PatchTableAsync(
                    datasetName,
                    tableName,
                    tableRef.Resource,
                    new PatchTableOptions
                    {
                    }).ConfigureAwait(false);
            }

            return tableRef;
        }

        private static bool FieldListIsEqual(IList<TableFieldSchema> old, IList<TableFieldSchema> @new)
        {
            if (old == null && @new == null)
            {
                return true;
            }
            if (old == null || @new == null)
            {
                return false;
            }
            if (old.Count != @new.Count)
            {
                return false;
            }

            var oldByKv = old.ToDictionary(k => k.Name, v => v);
            var newByKv = @new.ToDictionary(k => k.Name, v => v);

            foreach (var kv in oldByKv)
            {
                if (!newByKv.ContainsKey(kv.Key))
                {
                    return false;
                }
            }

            foreach (var kv in newByKv)
            {
                if (!oldByKv.TryGetValue(kv.Key, out TableFieldSchema? value))
                {
                    return false;
                }
                if (value.Name != kv.Value.Name)
                {
                    return false;
                }
                if (value.Description != kv.Value.Description)
                {
                    return false;
                }
                if (value.Mode != kv.Value.Mode)
                {
                    return false;
                }
                if (!FieldListIsEqual(value.Fields, kv.Value.Fields))
                {
                    return false;
                }
            }

            return true;
        }

        public async Task<BigQueryTable?> GetReadableTableForProject(Key projectKey, string table, int tableVersion)
        {
            ArgumentNullException.ThrowIfNull(projectKey);

            if (projectKey.Equals(PublicDatasetKey))
            {
                return await GetReadableTableForProject("public", table, tableVersion).ConfigureAwait(false);
            }

            return await GetReadableTableForProject(_globalPrefix.Create(projectKey), table, tableVersion).ConfigureAwait(false);
        }

        public async Task<BigQueryTable?> GetReadableTableForProject(string projectId, string table, int tableVersion)
        {
            ArgumentException.ThrowIfNullOrEmpty(projectId);

            var datasetName = projectId.Replace('-', '_');
            var tableName = table + "_v" + tableVersion;
            var cacheName = datasetName + "." + tableName;
            if (!_memoryCache.TryGetValue(cacheName, out BigQueryTable? tableRef))
            {
                try
                {
                    tableRef = await _client.GetTableAsync(datasetName, tableName).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Table might not exist, return null.
                    return null;
                }

                var schema = _knownSchemata[tableName];
                tableRef = await CheckTableRefMatchesSchemaAndUpdateIfNecessary(datasetName, tableName, tableRef, schema).ConfigureAwait(false);

                _memoryCache.Set(cacheName, tableRef, _memoryCacheOptions);
            }

            return tableRef;
        }

        public Task<BigQueryResultsInfo> ExecuteLegacyQuery(Key projectKey, string query)
        {
            return ExecuteLegacyQuery(projectKey, query, false);
        }

        public async Task<BigQueryResultsInfo> ExecuteLegacyQuery(Key projectKey, string query, bool disableCache)
        {
            var results = await _client.ExecuteQueryAsync(query, null, new QueryOptions
            {
                UseLegacySql = true,
                UseQueryCache = !disableCache,
            }).ConfigureAwait(false);

            var jobInfo = await _client.GetJobAsync(results.JobReference.JobId).ConfigureAwait(false);

            results.ThrowOnAnyError();

            return new BigQueryResultsInfo
            {
                Results = results,
                Job = jobInfo,
            };
        }

        public Task<BigQueryResultsInfo> ExecuteStandardQuery(Key projectKey, string query, params BigQueryParameter[] parameters)
        {
            return ExecuteStandardQuery(projectKey, query, false, parameters);
        }

        public async Task<BigQueryResultsInfo> ExecuteStandardQuery(Key projectKey, string query, bool disableCache, params BigQueryParameter[] parameters)
        {
            var results = await _client.ExecuteQueryAsync(query, parameters, new QueryOptions
            {
                UseLegacySql = false,
                UseQueryCache = !disableCache,
            }).ConfigureAwait(false);

            var jobInfo = await _client.GetJobAsync(results.JobReference.JobId).ConfigureAwait(false);

            results.ThrowOnAnyError();

            return new BigQueryResultsInfo
            {
                Results = results,
                Job = jobInfo,
            };
        }

        public string EscapeLegacyString(string str)
        {
            return Encoding.ASCII.GetBytes(str).Select(x => "\\x" + BitConverter.ToString(new[] { x }).ToLowerInvariant()).Aggregate((a, b) => a + b);
        }

        public async Task DeleteTableForProject(Key projectKey, string table, int tableVersion)
        {
            ArgumentNullException.ThrowIfNull(projectKey);

            var projectId = projectKey.Equals(PublicDatasetKey) ? "public" : _globalPrefix.Create(projectKey);
            var datasetName = projectId.Replace('-', '_');
            var tableName = table + "_v" + tableVersion;
            var cacheName = datasetName + "." + tableName;
            if (!_memoryCache.TryGetValue(cacheName, out BigQueryTable? tableRef))
            {
                try
                {
                    tableRef = await _client.GetTableAsync(datasetName, tableName).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Table might not exist, ignore.
                    return;
                }

                _memoryCache.Set(cacheName, tableRef);
            }
            if (tableRef == null)
            {
                // Table might not exist, ignore.
                return;
            }

            try
            {
                await _client.DeleteTableAsync(tableRef.Reference).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Not found: Table", StringComparison.InvariantCultureIgnoreCase))
                {
                    // Table already doesn't exist.
                }
                else
                {
                    throw;
                }
            }

            _memoryCache.Remove(cacheName);
        }

        public async Task<bool> GetTableExistsForProject(Key projectKey, string table, int? tableVersion)
        {
            ArgumentNullException.ThrowIfNull(projectKey);

            var projectId = projectKey.Equals(PublicDatasetKey) ? "public" : _globalPrefix.Create(projectKey);
            var datasetName = projectId.Replace('-', '_');
            var tableName = tableVersion == null ? table : (table + "_v" + tableVersion);
            BigQueryTable tableRef;
            try
            {
                tableRef = await _client.GetTableAsync(datasetName, tableName).ConfigureAwait(false);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
