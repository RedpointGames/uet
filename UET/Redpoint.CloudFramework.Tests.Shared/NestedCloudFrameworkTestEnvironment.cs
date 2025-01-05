namespace Redpoint.CloudFramework.Tests
{
    using System;

    internal class NestedCloudFrameworkTestEnvironment : ICloudFrameworkTestEnvironment
    {
        public NestedCloudFrameworkTestEnvironment(IServiceProvider serviceProvider)
        {
            Services = serviceProvider;
        }

        public IServiceProvider Services { get; }
    }
}
