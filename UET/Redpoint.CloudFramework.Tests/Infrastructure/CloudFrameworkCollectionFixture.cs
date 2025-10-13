namespace Redpoint.CloudFramework.Tests.Infrastructure
{
    using Microsoft.Extensions.Configuration;
    using Xunit;

    [CollectionDefinition("CloudFramework Test")]
    public class CloudFrameworkCollectionFixture : ICollectionFixture<CloudFrameworkTestEnvironment>
    {
    }

    [CollectionDefinition("CloudFramework Test")]
    public class CloudFrameworkCollectionFixture<TConfiguration> : ICollectionFixture<CloudFrameworkTestEnvironment<TConfiguration>> where TConfiguration : ICloudFrameworkTestEnvironmentConfiguration, new()
    {
    }
}
