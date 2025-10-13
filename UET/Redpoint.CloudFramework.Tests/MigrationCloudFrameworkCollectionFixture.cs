namespace Redpoint.CloudFramework.Tests
{
    using Redpoint.CloudFramework.Tests.Migration;
    using Xunit;

    [CollectionDefinition("Migration CloudFramework Test")]
    public class MigrationCloudFrameworkCollectionFixture : ICollectionFixture<CloudFrameworkTestEnvironment<MigrationCloudFrameworkTestEnvironmentConfiguration>>
    {
    }
}
