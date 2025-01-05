namespace Redpoint.CloudFramework.Repository.Transaction
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface IModelTransaction : IAsyncDisposable
    {
        /// <summary>
        /// The namespace the transaction is occurring in.
        /// </summary>
        string Namespace { get; }

        /// <summary>
        /// The transaction itself.
        /// </summary>
        internal DatastoreTransaction Transaction { get; }

        /// <summary>
        /// A list of models that have been modified by this transaction.
        /// </summary>
        IReadOnlyList<Model> ModifiedModels { get; }
        internal List<Model> ModifiedModelsList { get; }

        /// <summary>
        /// A list of queued operations to be performed immediately before
        /// Commit() is called for Datastore.
        /// </summary>
        IReadOnlyList<Func<Task>> QueuedPreCommitOperations { get; }
        internal List<Func<Task>> QueuedPreCommitOperationsList { get; }

        /// <summary>
        /// If true, this transaction has been committed.
        /// </summary>
        bool HasCommitted { get; internal set; }

        /// <summary>
        /// If true, this transaction has been rolled back explicitly.
        /// </summary>
        bool HasRolledBack { get; internal set; }

        /// <summary>
        /// If true, this transaction is a nested transaction and you can't call
        /// <see cref="IGlobalRepository.CommitAsync(string, IModelTransaction, Metrics.RepositoryOperationMetrics?, CancellationToken)"/>
        /// or <see cref="IGlobalRepository.RollbackAsync(string, IModelTransaction, Metrics.RepositoryOperationMetrics?, CancellationToken)"/>
        /// on it directly.
        /// </summary>
        bool IsNestedTransaction { get; }
    }
}
