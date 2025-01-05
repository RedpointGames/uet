namespace Redpoint.CloudFramework.Repository
{
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;

    public interface IRepository
    {
        IBatchedAsyncEnumerable<T> QueryAsync<T>(
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<PaginatedQueryResult<T>> QueryPaginatedAsync<T>(
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<T?> LoadAsync<T>(
            Key key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        IBatchedAsyncEnumerable<KeyValuePair<Key, T?>> LoadAsync<T>(
            IAsyncEnumerable<Key> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<T> CreateAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        IAsyncEnumerable<T> CreateAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<T> UpsertAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        IAsyncEnumerable<T> UpsertAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<T> UpdateAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        IAsyncEnumerable<T> UpdateAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task DeleteAsync<T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task DeleteAsync<T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<Key> AllocateKeyAsync<T>(
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<KeyFactory> GetKeyFactoryAsync<T>(
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : Model, new();

        Task<IModelTransaction> BeginTransactionAsync(
            TransactionMode mode = TransactionMode.ReadWrite,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default);

        Task CommitAsync(
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default);

        Task RollbackAsync(
            IModelTransaction transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default);
    }
}
