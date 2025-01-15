namespace Redpoint.CloudFramework.Repository
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IGlobalRepository
    {
        IBatchedAsyncEnumerable<T> QueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<PaginatedQueryResult<T>> QueryPaginatedAsync<T>(
            string @namespace,
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T?> LoadAsync<T>(
            string @namespace,
            Key key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IBatchedAsyncEnumerable<KeyValuePair<Key, T?>> LoadAsync<T>(
            string @namespace,
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<KeyValuePair<Key, T?>> LoadAcrossNamespacesAsync<T>(
            IAsyncEnumerable<Key> keys,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T> CreateAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<T> CreateAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T> UpsertAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<T> UpsertAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T> UpdateAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<T> UpdateAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task DeleteAsync<T>(
            string @namespace,
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task DeleteAsync<T>(
            string @namespace,
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<Key> AllocateKeyAsync<T>(
            string @namespace,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<KeyFactory> GetKeyFactoryAsync<T>(
            string @namespace,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<IModelTransaction> BeginTransactionAsync(
            string @namespace,
            TransactionMode mode = TransactionMode.ReadWrite,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default);

        Task CommitAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default);

        Task RollbackAsync(
            string @namespace,
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default);
    }
}
