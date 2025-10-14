namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    internal class NestedCloudFrameworkTestEnvironment : ICloudFrameworkTestEnvironment
    {
        public NestedCloudFrameworkTestEnvironment(IServiceProvider serviceProvider)
        {
            Services = serviceProvider;
        }

        public IServiceProvider Services { get; }

        public IServiceProvider CreateServiceProvider(Action<ServiceCollection> configure)
        {
            throw new NotImplementedException();
        }
    }
}
