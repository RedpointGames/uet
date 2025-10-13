namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.CloudFramework.Repository;
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

    [Collection("Migration CloudFramework Test")]
    public class MigrationTests
    {
        private readonly CloudFrameworkTestEnvironment<MigrationCloudFrameworkTestEnvironmentConfiguration> _env;

        public MigrationTests(CloudFrameworkTestEnvironment<MigrationCloudFrameworkTestEnvironmentConfiguration> env)
        {
            _env = env;
        }

        [Fact]
        public async Task TestMigrate()
        {
            var model = new MigrationModel
            {
                schemaVersion = 1,
                stringField = string.Empty,
            };

            var globalRepository = _env.Services.GetRequiredService<IGlobalRepository>();
            await globalRepository.CreateAsync(string.Empty, model, cancellationToken: TestContext.Current.CancellationToken);

            var allMigrators = _env.Services.GetServices<RegisteredModelMigratorBase>().ToArray();

            var logger = _env.Services.GetRequiredService<ILogger<MigrationTests>>();

            Assert.NotEmpty(allMigrators);
            var drl = _env.Services.GetRequiredService<IDatastoreRepositoryLayer>();

            var migratorsByModel = allMigrators.GroupBy(x => x.ModelType);
            foreach (var modelGroup in migratorsByModel)
            {
                logger.LogInformation($"Running database migrators for: {modelGroup.Key.FullName}");

                RegisteredModelMigratorBase[] migrators = modelGroup.ToArray();

                var executor = _env.Services.GetService(modelGroup.First().ExecutorType) as IModelMigratorExecutor;
                Assert.NotNull(executor);
                await executor.ExecuteMigratorsAsync(migrators, TestContext.Current.CancellationToken);
            }
        }
    }
}
