namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;

    public interface IModelMigrator<T> : INonTransactionalModelMigrator<T> where T : IModel
    {
        /// <summary>
        /// Migrate the specified model to the version this migrator was registered for. Returns true if the model had
        /// UpdateAsync called on it as part of the migration.
        /// </summary>
        /// <param name="model">The model to migration.</param>
        /// <returns>Returns true if the model had UpdateAsync called on it as part of the migration.</returns>
        new Task<bool> MigrateAsync(T model);

        async Task<NonTransactionalModelMigrationResult> INonTransactionalModelMigrator<T>.MigrateAsync(T model)
        {
            if (await MigrateAsync(model).ConfigureAwait(false))
            {
                return NonTransactionalModelMigrationResult.Updated;
            }
            else
            {
                return NonTransactionalModelMigrationResult.Changed;
            }
        }
    }
}
