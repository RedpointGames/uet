namespace Redpoint.CloudFramework.Tests.Migration
{
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Tests.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Xunit;

    internal class MigrationTestToVersion2Migrator : INonTransactionalModelMigrator<MigrationModel>
    {
        private readonly IDesiredSchemaVersion<MigrationTestToVersion2Migrator> _desiredSchemaVersion;

        public MigrationTestToVersion2Migrator(
            IDesiredSchemaVersion<MigrationTestToVersion2Migrator> desiredSchemaVersion)
        {
            _desiredSchemaVersion = desiredSchemaVersion;
        }

        public Task<NonTransactionalModelMigrationResult> MigrateAsync(MigrationModel model)
        {
            Assert.Equal(string.Empty, model.stringField);
            Assert.Equal(1, model.schemaVersion);
            Assert.Equal(2, _desiredSchemaVersion.SchemaVersion);
            model.stringField = "version2";
            return Task.FromResult(NonTransactionalModelMigrationResult.Changed);
        }
    }
}
