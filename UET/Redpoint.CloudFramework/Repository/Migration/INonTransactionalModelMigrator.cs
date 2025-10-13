namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Transaction;
    using System.Threading.Tasks;

    public interface INonTransactionalModelMigrator<T> : IRegisterableModelMigrator<T> where T : IModel
    {
        /// <summary>
        /// Migrate the specified model to the version this migrator was registered for. The migration system
        /// already updates <see cref="IModel.schemaVersion"/> for you.
        /// 
        /// Returns <see cref="NonTransactionalModelMigrationResult.Updated"/> 
        /// if the migrator called <see cref="IGlobalRepository.UpdateAsync{T}(string, T, IModelTransaction?, Metrics.RepositoryOperationMetrics?, CancellationToken)"/> on the model as part of the migration.
        /// </summary>
        /// <param name="model">The model to migrate.</param>
        /// <returns>Returns <see cref="NonTransactionalModelMigrationResult.Updated"/> 
        /// if the migrator called <see cref="IGlobalRepository.UpdateAsync{T}(string, T, IModelTransaction?, Metrics.RepositoryOperationMetrics?, CancellationToken)"/> on the model as part of the migration.</returns>
        Task<NonTransactionalModelMigrationResult> MigrateAsync(T model);
    }
}
