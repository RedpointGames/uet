namespace Redpoint.CloudFramework.Repository.Datastore
{
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.Configuration;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Converters.Timestamp;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.Collections.Batching;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DatastoreGlobalRepository : IGlobalRepository
    {
        private readonly IRedisCacheRepositoryLayer? _redisCacheRepositoryLayer;
        private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;

        // NOTE: This is used by the legacy extension methods in GlobalRepositoryLegacyExtensions.
        internal readonly IInstantTimestampConverter _instantTimestampConverter;

        public DatastoreGlobalRepository(
            IDatastoreRepositoryLayer datastoreRepositoryLayer,
            IInstantTimestampConverter instantTimestampConverter,
            IConfiguration configuration,
            IRedisCacheRepositoryLayer? redisCacheRepositoryLayer = null)
        {
            _redisCacheRepositoryLayer = redisCacheRepositoryLayer;
            _datastoreRepositoryLayer = datastoreRepositoryLayer;
            _instantTimestampConverter = instantTimestampConverter;
        }

        internal IRepositoryLayer Layer
        {
            get
            {
                return _redisCacheRepositoryLayer ?? (IRepositoryLayer)_datastoreRepositoryLayer;
            }
        }

        public IBatchedAsyncEnumerable<T> QueryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.QueryAsync(@namespace, where, order, limit, transaction, metrics, cancellationToken);
        }

        public Task<PaginatedQueryResult<T>> QueryPaginatedAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.QueryPaginatedAsync(@namespace, cursor, limit, where, order, transaction, metrics, cancellationToken);
        }

        public Task<T?> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            Key<T> key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.LoadAsync<T>(@namespace, key, transaction, metrics, cancellationToken);
        }

        public IBatchedAsyncEnumerable<KeyValuePair<Key<T>, T?>> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            IAsyncEnumerable<Key<T>> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.LoadAsync<T>(@namespace, keys, transaction, metrics, cancellationToken);
        }

        public IAsyncEnumerable<KeyValuePair<Key<T>, T?>> LoadAcrossNamespacesAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<Key<T>> keys,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.LoadAcrossNamespacesAsync<T>(keys, metrics, cancellationToken);
        }

        public async Task<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await Layer.CreateAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.CreateAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public async Task<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await Layer.UpsertAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.UpsertAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public async Task<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await Layer.UpdateAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public IAsyncEnumerable<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.UpdateAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.DeleteAsync(@namespace, new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken);
        }

        public Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.DeleteAsync(@namespace, models, transaction, metrics, cancellationToken);
        }

        public Task<Key<T>> AllocateKeyAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return Layer.AllocateKeyAsync<T>(@namespace, transaction, metrics, cancellationToken);
        }

        public Task<KeyFactory<T>> GetKeyFactoryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
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
