namespace Redpoint.CloudFramework.Repository.Layers
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.Collections.Batching;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal interface IRepositoryLayer
    {
        /// <summary>
        /// Fired when non-transactional entities are updated by this repository layer, and the parent layers should
        /// flush any appropriate caches.
        /// 
        /// Only non-transactional entities have this event fired, since the parent layer will be able to determine
        /// when entities in transactions are affected since it will also be handling the CommitAsync() function.
        /// </summary>
        AsyncEvent<EntitiesModifiedEventArgs> OnNonTransactionalEntitiesModified { get; }

        /// <summary>
        /// Executes a query asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of model to query for.</typeparam>
        /// <param name="namespace">The namespace the model is located in; use string.Empty for the default namespace.</param>
        /// <param name="where">
        /// The filter that must be met for the returned models. You can build composite queries using &amp;&amp;, and filter
        /// on properties using the ==, less/greater than and less/greater or equal than operators.
        /// 
        /// You can't use != or || operators in the where clause.
        /// 
        /// If you want to add a "has ancestor" clause, use an expression like 'x.Key.HasAncestor(ancestor)', using
        /// the <c>HasAncestor</c> extension method in <c>Redpoint.CloudFramework.Repository.RepositoryExtensions</c>. The
        /// <c>HasAncestor</c> extension method can also be used outside queries if you want to evaluate ancestors
        /// on the client.
        /// 
        /// If you want to retrieve every model, use an expression like 'x =&gt; true'.
        /// </param>
        /// <param name="order">
        /// The sort order for the returned models. You can specify multiple sort orders using the bitwise | operator. 
        /// For example, the expression <c>x.first &gt; x.first | x.second &lt; x.second</c> means 
        /// "sort by 'first' descending, then sort by 'second' ascending".
        /// </param>
        /// <param name="limit">
        /// The limit on the number of models to return. If null, an unlimited number of models can be fetched.
        /// </param>
        /// <param name="transaction">The transaction this query is part of.</param>
        /// <param name="metrics">The metrics object to report to, or null if metrics data should not be tracked.</param>
        /// <param name="cancellationToken">The cancellation token for the asynchronous operation.</param>
        /// <returns>An asynchronous enumerable that you can iterate over to receive results.</returns>
        IBatchedAsyncEnumerable<T> QueryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new();

        Task<PaginatedQueryResult<T>> QueryPaginatedAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(
            string @namespace,
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : class, IModel, new();

        Task<T?> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, Key<T> key, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        IBatchedAsyncEnumerable<KeyValuePair<Key<T>, T?>> LoadAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, IAsyncEnumerable<Key<T>> keys, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        IAsyncEnumerable<KeyValuePair<Key<T>, T?>> LoadAcrossNamespacesAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(IAsyncEnumerable<Key<T>> keys, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        IAsyncEnumerable<T> CreateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        IAsyncEnumerable<T> UpsertAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        IAsyncEnumerable<T> UpdateAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        Task DeleteAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        Task<Key<T>> AllocateKeyAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        Task<KeyFactory<T>> GetKeyFactoryAsync<[DynamicallyAccessedMembers(DynamicReferencePolicy.ModelPolicy)] T>(string @namespace, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : class, IModel, new();

        Task<IModelTransaction> BeginTransactionAsync(string @namespace, TransactionMode mode, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken);

        Task CommitAsync(string @namespace, IModelTransaction transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken);

        Task RollbackAsync(string @namespace, IModelTransaction transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken);
    }
}
