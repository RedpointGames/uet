namespace Redpoint.CloudFramework.Tests.Migration
{
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Tests.Models;
    using System.Threading.Tasks;
    using Xunit;

    internal class MigrationTestToVersion4Migrator : INonTransactionalModelMigrator<MigrationModel>
    {
        private readonly IGlobalRepository _globalRepository;
        private readonly IDesiredSchemaVersion<MigrationTestToVersion4Migrator> _desiredSchemaVersion;

        public MigrationTestToVersion4Migrator(
            IGlobalRepository globalRepository,
            IDesiredSchemaVersion<MigrationTestToVersion4Migrator> desiredSchemaVersion)
        {
            _globalRepository = globalRepository;
            _desiredSchemaVersion = desiredSchemaVersion;
        }

        public async Task<NonTransactionalModelMigrationResult> MigrateAsync(MigrationModel model)
        {
            Assert.Equal("version3", model.stringField);
            Assert.Equal(3, model.schemaVersion);
            Assert.Equal(4, _desiredSchemaVersion.SchemaVersion);
            model.stringField = "version4";
            model.schemaVersion = _desiredSchemaVersion.SchemaVersion;
            await _globalRepository.UpdateAsync(
                string.Empty,
                model);
            return NonTransactionalModelMigrationResult.Updated;
        }
    }
}
