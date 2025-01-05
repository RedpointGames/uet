namespace Redpoint.CloudFramework.Repository.Layers
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Collections.Batching;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using Redpoint.CloudFramework.Repository.Pagination;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.Concurrency;
    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Threading.Tasks;

    internal class EntitiesModifiedEventArgs : EventArgs
    {
        /// <summary>
        /// The keys that were modified or deleted.
        /// </summary>
        public required Key[] Keys { get; init; }

        /// <summary>
        /// If there were metrics passed into the original operation that caused this event to be
        /// raised, this is the metrics object that was passed in.
        /// </summary>
        public required RepositoryOperationMetrics? Metrics { get; init; }
    }

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
        IBatchedAsyncEnumerable<T> QueryAsync<T>(
            string @namespace,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            int? limit,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : Model, new();

        Task<PaginatedQueryResult<T>> QueryPaginatedAsync<T>(
            string @namespace,
            PaginatedQueryCursor cursor,
            int limit,
            Expression<Func<T, bool>> where,
            Expression<Func<T, bool>>? order,
            IModelTransaction? transaction,
            RepositoryOperationMetrics? metrics,
            CancellationToken cancellationToken) where T : Model, new();

        Task<T?> LoadAsync<T>(string @namespace, Key key, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        IBatchedAsyncEnumerable<KeyValuePair<Key, T?>> LoadAsync<T>(string @namespace, IAsyncEnumerable<Key> keys, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        IAsyncEnumerable<KeyValuePair<Key, T?>> LoadAcrossNamespacesAsync<T>(IAsyncEnumerable<Key> keys, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        IAsyncEnumerable<T> CreateAsync<T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        IAsyncEnumerable<T> UpsertAsync<T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        IAsyncEnumerable<T> UpdateAsync<T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        Task DeleteAsync<T>(string @namespace, IAsyncEnumerable<T> models, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        Task<Key> AllocateKeyAsync<T>(string @namespace, IModelTransaction? transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        Task<KeyFactory> GetKeyFactoryAsync<T>(string @namespace, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken) where T : Model, new();

        Task<IModelTransaction> BeginTransactionAsync(string @namespace, TransactionMode mode, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken);

        Task CommitAsync(string @namespace, IModelTransaction transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken);

        Task RollbackAsync(string @namespace, IModelTransaction transaction, RepositoryOperationMetrics? metrics, CancellationToken cancellationToken);
    }
}
