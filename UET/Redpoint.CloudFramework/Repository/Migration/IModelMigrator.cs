namespace Redpoint.CloudFramework.Repository.Migration
{
    using Redpoint.CloudFramework.Models;
    using System.Threading.Tasks;

    public interface IModelMigrator<T> where T : IModel
    {
        /// <summary>
        /// Migrate the specified model to the version this migrator was registered for. Returns true if the model had
        /// UpdateAsync called on it as part of the migration.
        /// </summary>
        /// <param name="model">The model to migration.</param>
        /// <returns>Returns true if the model had UpdateAsync called on it as part of the migration.</returns>
        Task<bool> MigrateAsync(T model);
    }
}
