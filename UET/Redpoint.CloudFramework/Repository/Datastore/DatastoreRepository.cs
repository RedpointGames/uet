namespace Redpoint.CloudFramework.Repository.Datastore
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System;
    using System.Collections.Generic;
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

        public IBatchedAsyncEnumerable<T> QueryAsync<T>(
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
            => BatchedQueryAsync(where, order, limit, transaction, metrics, cancellationToken).AsBatchedAsyncEnumerable();

        private async IAsyncEnumerable<IReadOnlyList<T>> BatchedQueryAsync<T>(
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : Model, new()
        {
            await foreach (var batch in _globalDatastore.QueryAsync(await GetDatastoreNamespace().ConfigureAwait(false), where, order, limit, transaction, metrics, cancellationToken).AsBatches().ConfigureAwait(false))
            {
                yield return batch;
            }
        }

        public async Task<PaginatedQueryResult<T>> QueryPaginatedAsync<T>(
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await _globalDatastore.QueryPaginatedAsync(await GetDatastoreNamespace().ConfigureAwait(false), cursor, limit, where, order, transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<T?> LoadAsync<T>(
            Key key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await _globalDatastore.LoadAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), key, transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public IBatchedAsyncEnumerable<KeyValuePair<Key, T?>> LoadAsync<T>(
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new() =>
            BatchedLoadAsync<T>(keys, transaction, metrics, cancellationToken).AsBatchedAsyncEnumerable();

        public async IAsyncEnumerable<IReadOnlyList<KeyValuePair<Key, T?>>> BatchedLoadAsync<T>(
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : Model, new()
        {
            await foreach (var batch in _globalDatastore.LoadAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), keys, transaction, metrics, cancellationToken).AsBatches().ConfigureAwait(false))
            {
                yield return batch;
            }
        }

        public async Task<T> CreateAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await _globalDatastore.CreateAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<T> CreateAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : Model, new()
        {
            await foreach (var value in _globalDatastore.CreateAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false))
            {
                yield return value;
            }
        }

        public async Task<T> UpsertAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await _globalDatastore.UpsertAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<T> UpsertAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : Model, new()
        {
            await foreach (var value in _globalDatastore.UpsertAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false))
            {
                yield return value;
            }
        }

        public async Task<T> UpdateAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await _globalDatastore.UpdateAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).FirstAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async IAsyncEnumerable<T> UpdateAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : Model, new()
        {
            await foreach (var value in _globalDatastore.UpdateAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false))
            {
                yield return value;
            }
        }

        public async Task DeleteAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            await _globalDatastore.DeleteAsync(await GetDatastoreNamespace().ConfigureAwait(false), new[] { model }.ToAsyncEnumerable(), transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task DeleteAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            await _globalDatastore.DeleteAsync(await GetDatastoreNamespace().ConfigureAwait(false), models, transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<Key> AllocateKeyAsync<T>(
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
        {
            return await _globalDatastore.AllocateKeyAsync<T>(await GetDatastoreNamespace().ConfigureAwait(false), transaction, metrics, cancellationToken).ConfigureAwait(false);
        }

        public async Task<KeyFactory> GetKeyFactoryAsync<T>(
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new()
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
