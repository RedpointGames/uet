namespace Redpoint.CloudFramework.Repository.Transaction
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal sealed class NestedModelTransaction : IModelTransaction
    {
        private readonly IModelTransaction _transaction;

        internal NestedModelTransaction(
            IModelTransaction transaction)
        {
            _transaction = transaction;
        }

        public string Namespace => _transaction.Namespace;

        public DatastoreTransaction Transaction => _transaction.Transaction;

        public IReadOnlyList<IModel> ModifiedModels => _transaction.ModifiedModels;
        public List<IModel> ModifiedModelsList => _transaction.ModifiedModelsList;

        public IReadOnlyList<Func<Task>> QueuedPreCommitOperations => _transaction.QueuedPreCommitOperations;
        public List<Func<Task>> QueuedPreCommitOperationsList => _transaction.QueuedPreCommitOperationsList;

        public bool HasCommitted
        {
            get => _transaction.HasCommitted;
            set => _transaction.HasCommitted = value;
        }

        public bool HasRolledBack
        {
            get => _transaction.HasRolledBack;
            set => _transaction.HasRolledBack = value;
        }

        public bool IsNestedTransaction => true;

        public ValueTask DisposeAsync()
        {
            // Nested transactions do not auto-rollback on disposal.
            return ValueTask.CompletedTask;
        }
    }
}
