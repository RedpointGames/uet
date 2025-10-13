using Xunit;

[assembly: CaptureConsole]
[assembly: CaptureTrace]

namespace Redpoint.CloudFramework.Tests
{
    using Google.Cloud.Datastore.V1;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Repository;
    using Redpoint.CloudFramework.Repository.Converters.Model;
    using Redpoint.CloudFramework.Repository.Layers;
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Tests.Migration;
    using Redpoint.CloudFramework.Tests.Models;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;

    [Collection("CloudFramework Test")]
    public class MigrationTests
    {
        private readonly CloudFrameworkTestEnvironment _env;

        public MigrationTests(CloudFrameworkTestEnvironment env)
        {
            _env = env;
        }

        [Fact]
        public async Task TestMigrate()
        {
            var services = _env.CreateServiceProvider(services =>
            {
                services.AddMigration<MigrationModel, MigrationTestToVersion2Migrator>(2);
                services.AddMigration<MigrationModel, MigrationTestToVersion3Migrator>(3);
                services.AddMigration<MigrationModel, MigrationTestToVersion4Migrator>(4);
            });

            var model = new MigrationModel
            {
                stringField = string.Empty,
            };

            var globalRepository = services.GetRequiredService<IGlobalRepository>();
            await globalRepository.CreateAsync(string.Empty, model, cancellationToken: TestContext.Current.CancellationToken);

            // Explicitly rollback the schema version, since CreateAsync will always set it to the latest version.
            model.schemaVersion = 1;
            await globalRepository.UpdateAsync(string.Empty, model, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal(1, model.schemaVersion);
            Assert.Equal(string.Empty, model.stringField);

            var allMigrators = services.GetServices<RegisteredModelMigratorBase>().ToArray();

            var logger = services.GetRequiredService<ILogger<MigrationTests>>();

            Assert.NotEmpty(allMigrators);
            var drl = services.GetRequiredService<IDatastoreRepositoryLayer>();

            var migratorsByModel = allMigrators.GroupBy(x => x.ModelType);
            foreach (var modelGroup in migratorsByModel)
            {
                logger.LogInformation($"Running database migrators for: {modelGroup.Key.FullName}");

                RegisteredModelMigratorBase[] migrators = modelGroup.ToArray();

                var executor = services.GetService(modelGroup.First().ExecutorType) as IModelMigratorExecutor;
                Assert.NotNull(executor);
                await executor.ExecuteMigratorsAsync(migrators, TestContext.Current.CancellationToken);
            }

            await DatastoreRepositoryLayerTests.HandleEventualConsistency(async () =>
            {
                model = await globalRepository.LoadAsync<MigrationModel>(string.Empty, model.Key, cancellationToken: TestContext.Current.CancellationToken);
                Assert.NotNull(model);
                Assert.Equal(4, model.schemaVersion);
                Assert.Equal("version4", model.stringField);
            });
        }
    }
}