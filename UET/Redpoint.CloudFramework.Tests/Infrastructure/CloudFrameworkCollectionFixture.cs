namespace Redpoint.CloudFramework.Tests.Infrastructure
{
    using Xunit;

    [CollectionDefinition("CloudFramework Test")]
    public class CloudFrameworkCollectionFixture : ICollectionFixture<CloudFrameworkTestEnvironment>
    {
    }
}
