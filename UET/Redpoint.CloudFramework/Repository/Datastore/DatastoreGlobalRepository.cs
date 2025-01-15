namespace Redpoint.CloudFramework.Repository.Datastore
{
    using Google.Cloud.Datastore.V1;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Redpoint.CloudFramework.Models;
    using System.Threading;
    using System.Linq.Expressions;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Microsoft.Extensions.Configuration;
    using Redpoint.CloudFramework.Collections.Batching;

    internal class DatastoreGlobalRepository : IGlobalRepository
    {
        private readonly IRedisCacheRepositoryLayer _redisCacheRepositoryLayer;
        private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;
        private readonly IConfiguration _configuration;

        // NOTE: This is used by the legacy extension methods in GlobalRepositoryLegacyExtensions.
        internal readonly IInstantTimestampConverter _instantTimestampConverter;

        public DatastoreGlobalRepository(
            IRedisCacheRepositoryLayer redisCacheRepositoryLayer,
            IDatastoreRepositoryLayer datastoreRepositoryLayer,
            IInstantTimestampConverter instantTimestampConverter,
            IConfiguration configuration)
        {
            _redisCacheRepositoryLayer = redisCacheRepositoryLayer;
            _datastoreRepositoryLayer = datastoreRepositoryLayer;
            _instantTimestampConverter = instantTimestampConverter;
            _configuration = configuration;
        }

        internal IRepositoryLayer Layer
        {
            get
            {
                return _redisCacheRepositoryLayer;
            }
        }

        public IBatchedAsyncEnumerable<T> QueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.QueryAsync(@namespace, where, order, limit, transaction, metrics, cancellationToken);
        }

        public Task<PaginatedQueryResult<T>> QueryPaginatedAsync<T>(
            string @namespace,
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.QueryPaginatedAsync(@namespace, cursor, limit, where, order, transaction, metrics, cancellationToken);
        }

        public Task<T?> LoadAsync<T>(
            string @namespace,
            Key key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.LoadAsync<T>(@namespace, key, transaction, metrics, cancellationToken);
        }

        public IBatchedAsyncEnumerable<KeyValuePair<Key, T?>> LoadAsync<T>(
            string @namespace,
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.LoadAsync<T>(@namespace, keys, transaction, metrics, cancellationToken);
        }

        public IAsyncEnumerable<KeyValuePair<Key, T?>> LoadAcrossNamespacesAsync<T>(
            IAsyncEnumerable<Key> keys,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.LoadAcrossNamespacesAsync<T>(keys, metrics, cancellationToken);
        }

        public async Task<T> CreateAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await Layer.CreateAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> CreateAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.CreateAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public async Task<T> UpsertAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await Layer.UpsertAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> UpsertAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.UpsertAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public async Task<T> UpdateAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await Layer.UpdateAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> UpdateAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.UpdateAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public Task DeleteAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.DeleteAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken);
        }

        public Task DeleteAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.DeleteAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public Task<Key> AllocateKeyAsync<T>(
            string @namespace,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.AllocateKeyAsync<T>(@namespace, transaction, metrics, cancellationToken);
        }

        public Task<KeyFactory> GetKeyFactoryAsync<T>(
            string @namespace,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return Layer.GetKeyFactoryAsync<T>(@namespace, metrics, cancellationToken);
        }

        public Task<IModelTransaction> BeginTransactionAsync(
            string @namespace,
            TransactionMode mode = TransactionMode.ReadWrite,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            return Layer.BeginTransactionAsync(@namespace, mode, metrics, cancellationToken);
        }

        public Task CommitAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            return Layer.CommitAsync(@namespace, transaction, metrics, cancellationToken);
        }

        public Task RollbackAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            return Layer.RollbackAsync(@namespace, transaction, metrics, cancellationToken);
        }
    }
}
