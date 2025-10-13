namespace Redpoint.CloudFramework.Tests.Migration
{
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Repository.Transaction;
    using Redpoint.CloudFramework.Tests.Models;
    using System.Threading.Tasks;
    using Xunit;

    internal class MigrationTestToVersion3Migrator : ITransactionalModelMigrator<MigrationModel>
    {
        private readonly IGlobalRepository _globalRepository;

        public MigrationTestToVersion3Migrator(
            IGlobalRepository globalRepository)
        {
            _globalRepository = globalRepository;
        }

        public async Task MigrateAsync(
            MigrationModel model, 
            IModelTransaction transaction)
        {
            Assert.Equal("version2", model.stringField);
            Assert.Equal(2, model.schemaVersion);
            model.stringField = "version3";
            model.schemaVersion = 3;
            await _globalRepository.UpdateAsync(
                string.Empty,
                model,
                transaction).ConfigureAwait(false);
            await _globalRepository.CommitAsync(
                string.Empty,
                transaction).ConfigureAwait(false);
        }
    }
}
