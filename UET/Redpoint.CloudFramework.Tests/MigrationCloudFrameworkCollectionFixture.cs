namespace Redpoint.CloudFramework.Tests
{
    using Redpoint.CloudFramework.Tests.Migration;
    using Xunit;

    [CollectionDefinition("CloudFramework Test")]
    public class MigrationCloudFrameworkCollectionFixture : ICollectionFixture<CloudFrameworkTestEnvironment<MigrationCloudFrameworkTestEnvironmentConfiguration>>
    {
    }
}
