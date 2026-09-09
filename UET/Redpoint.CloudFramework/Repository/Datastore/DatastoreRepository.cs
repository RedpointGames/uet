namespace Redpoint.CloudFramework.Repository.Datastore
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.Collections.Batching;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DatastoreRepository : IRepository
    {
        internal readonly IGlobalRepository _globalDatastore;
        private readonly ICurrentTenantService _currentTenant;

        public DatastoreRepository(IGlobalRepository globalDatastore, ICurrentTenantService currentTenant)
        {
            _globalDatastore = globalDatastore;
            _currentTenant = currentTenant;
        }

        internal async Task<string> GetDatastoreNamespace()
        {
            var currentTenant = await _currentTenant.GetTenant().ConfigureAwait(false);
            if (currentTenant == null)
            {
                throw new InvalidOperationException("IRepository can not be used without a tenant.");
            }
            return currentTenant.DatastoreNamespace;
        }

        public IBatchedAsyncEnumerable<T> QueryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
            => BatchedQueryAsync(where, order, limit, transaction, metrics, cancellationToken).AsBatchedAsyncEnumerable();

        private async IAsyncEnumerable<IReadOnlyList<T>> BatchedQueryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await foreach (var batch in _globalDatastore.QueryAsync(await GetDatastoreNamespace().ConfigureAwait(false), where, order, limit, transaction, metrics, cancellationToken).AsBatches().ConfigureAwait(false))
            {
                yield return batch;
            }
        }

        public async Task<PaginatedQueryResult<T>> QueryPaginatedAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.QueryPaginatedAsync(await GetDatastoreNamespace().ConfigureAwait(false), cursor, limit, where, order, transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<T?> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            Key<T> key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.LoadAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), key, transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public IBatchedAsyncEnumerable<KeyValuePair<Key<T>, T?>> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<Key<T>> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new() =>
            BatchedLoadAsync<T>(keys, transaction, metrics, cancellationToken).AsBatchedAsyncEnumerable();

        public async IAsyncEnumerable<IReadOnlyList<KeyValuePair<Key<T>, T?>>> BatchedLoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<Key<T>> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await foreach (var batch in _globalDatastore.LoadAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), keys, transaction, metrics, cancellationToken).AsBatches().ConfigureAwait(false))
            {
                yield return batch;
            }
        }

        public async Task<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.CreateAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await foreach (var value in _globalDatastore.CreateAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false))
            {
                yield return value;
            }
        }

        public async Task<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.UpsertAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await foreach (var value in _globalDatastore.UpsertAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false))
            {
                yield return value;
            }
        }

        public async Task<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.UpdateAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await foreach (var value in _globalDatastore.UpdateAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false))
            {
                yield return value;
            }
        }

        public async Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await _globalDatastore.DeleteAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            await _globalDatastore.DeleteAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Key<T>> AllocateKeyAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.AllocateKeyAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<KeyFactory<T>> GetKeyFactoryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new()
        {
            return await _globalDatastore.GetKeyFactoryAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IModelTransaction> BeginTransactionAsync(
            TransactionMode mode = TransactionMode.ReadWrite,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            return await _globalDatastore.BeginTransactionAsync(await GetDatastoreNamespace().ConfigureAwait(false), mode, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task CommitAsync(
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            await _globalDatastore.CommitAsync(await GetDatastoreNamespace().ConfigureAwait(false), transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task RollbackAsync(
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default)
        {
            await _globalDatastore.RollbackAsync(await GetDatastoreNamespace().ConfigureAwait(false), transaction, metrics, cancellationToken).ConfigureAwait(false);
        }
    }
}
