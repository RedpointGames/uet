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

        public MigrationTestToVersion4Migrator(
            IGlobalRepository globalRepository)
        {
            _globalRepository = globalRepository;
        }

        public async Task<NonTransactionalModelMigrationResult> MigrateAsync(MigrationModel model)
        {
            Assert.Equal(string.Empty, model.stringField);
            Assert.Equal(3, model.schemaVersion);
            model.stringField = "version4";
            model.schemaVersion = 4;
            await _globalRepository.UpdateAsync(
                string.Empty,
                model);
            return NonTransactionalModelMigrationResult.Updated;
        }
    }
}
