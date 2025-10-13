namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System.Threading.Tasks;

    public interface ITransactionalModelMigrator<T> : IRegisterableModelMigrator<T> where T : IModel
    {
        /// <summary>
        /// Migrate the specified model to the version this migrator was registered for. The migration system
        /// already updates <see cref="IModel.schemaVersion"/> for you.
        /// 
        /// The implementation must call <see cref="IGlobalRepository.UpdateAsync{T}(string, T, IModelTransaction?, Metrics.RepositoryOperationMetrics?, CancellationToken)"/> and commit the transaction with <see cref="IGlobalRepository.CommitAsync(string, IModelTransaction, Metrics.RepositoryOperationMetrics?, CancellationToken)"/> for the migration to complete.
        /// </summary>
        /// <param name="model">The model to migrate.</param>
        /// <param name="transaction">The transaction.</param>
        /// <returns>Returns the awaitable task.</returns>
        Task MigrateAsync(T model, IModelTransaction transaction);
    }
}
