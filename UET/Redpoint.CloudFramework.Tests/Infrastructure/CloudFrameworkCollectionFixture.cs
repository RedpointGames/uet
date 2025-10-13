namespace Redpoint.CloudFramework.Tests.Infrastructure
{
    using Microsoft.Extensions.Configuration;
    using Xunit;

    [CollectionDefinition("CloudFramework Test")]
    public class CloudFrameworkCollectionFixture : ICollectionFixture<CloudFrameworkTestEnvironment>
    {
    }
}
