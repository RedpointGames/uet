namespace Redpoint.CloudFramework.Tests.Migration
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Repository.Migration;
    using Redpoint.CloudFramework.Tests.Models;

    public class MigrationCloudFrameworkTestEnvironmentConfiguration : ICloudFrameworkTestEnvironmentConfiguration
    {
        public void RegisterServices(IServiceCollection services)
        {
            services.AddMigration<MigrationModel, MigrationTestToVersion2Migrator>(2);
            services.AddMigration<MigrationModel, MigrationTestToVersion3Migrator>(3);
            services.AddMigration<MigrationModel, MigrationTestToVersion4Migrator>(4);
        }
    }
}
