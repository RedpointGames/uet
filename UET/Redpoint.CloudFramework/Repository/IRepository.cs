namespace Redpoint.CloudFramework.Repository
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
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IRepository
    {
        IBatchedAsyncEnumerable<T> QueryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            int? limit = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<PaginatedQueryResult<T>> QueryPaginatedAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order = null,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T?> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            Key<T> key,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IBatchedAsyncEnumerable<KeyValuePair<Key<T>, T?>> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<Key<T>> keys,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        IAsyncEnumerable<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            T model,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IAsyncEnumerable<T> models,
            IModelTransaction? transaction = null,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<Key<T>> AllocateKeyAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

        Task<KeyFactory<T>> GetKeyFactoryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            RepositoryOperationMetrics? metrics = null,
            CancellationToken cancellationToken = default) where T : class, IModel, new();

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
