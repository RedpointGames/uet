namespace Redpoint.CloudFramework.Repository.Migration
{
    public enum NonTransactionalModelMigrationResult
    {
        /// <summary>
        /// Changes were made to the model, but it wasn't updated with datastore; i.e. <see cref="IGlobalRepository.UpdateAsync{T}(string, T, Transaction.IModelTransaction?, Metrics.RepositoryOperationMetrics?, CancellationToken)"/> was not called.
        /// </summary>
        Changed,

        /// <summary>
        /// The model was updated by the model migrator.
        /// </summary>
        Updated,
    }
}
