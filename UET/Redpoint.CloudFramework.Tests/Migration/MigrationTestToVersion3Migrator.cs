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
        private readonly IDesiredSchemaVersion<MigrationTestToVersion3Migrator> _desiredSchemaVersion;

        public MigrationTestToVersion3Migrator(
            IGlobalRepository globalRepository,
            IDesiredSchemaVersion<MigrationTestToVersion3Migrator> desiredSchemaVersion)
        {
            _globalRepository = globalRepository;
            _desiredSchemaVersion = desiredSchemaVersion;
        }

        public async Task MigrateAsync(
            MigrationModel model, 
            IModelTransaction transaction)
        {
            Assert.Equal("version2", model.stringField);
            Assert.Equal(2, model.schemaVersion);
            Assert.Equal(3, _desiredSchemaVersion.SchemaVersion);
            model.stringField = "version3";
            model.schemaVersion = _desiredSchemaVersion.SchemaVersion;
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
