namespace Redpoint.CloudFramework.Repository.Transaction
{
    using Google.Cloud.Datastore.V1;
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Layers;
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class TopLevelModelTransaction : IModelTransaction
    {
        private readonly IDatastoreRepositoryLayer _datastoreRepositoryLayer;

        internal TopLevelModelTransaction(
            string @namespace,
            DatastoreTransaction transaction,
            IDatastoreRepositoryLayer datastoreRepositoryLayer)
        {
            _datastoreRepositoryLayer = datastoreRepositoryLayer;

            Namespace = @namespace;
            Transaction = transaction;
            ModifiedModelsList = new List<IModel>();
            QueuedPreCommitOperationsList = new List<Func<Task>>();
            HasCommitted = false;
            HasRolledBack = false;
        }

        public string Namespace { get; }

        public DatastoreTransaction Transaction { get; }

        public IReadOnlyList<IModel> ModifiedModels => ModifiedModelsList;
        public List<IModel> ModifiedModelsList { get; }

        public IReadOnlyList<Func<Task>> QueuedPreCommitOperations => QueuedPreCommitOperationsList;
        public List<Func<Task>> QueuedPreCommitOperationsList { get; }

        public bool HasCommitted { get; set; }

        public bool HasRolledBack { get; set; }

        public bool IsNestedTransaction => false;

        public async ValueTask DisposeAsync()
        {
            if (!HasCommitted && !HasRolledBack)
            {
                await _datastoreRepositoryLayer.RollbackAsync(Namespace, this, null, CancellationToken.None).ConfigureAwait(false);
            }
        }
    }
}
