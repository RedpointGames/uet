namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System;

    public interface ICloudFrameworkTestEnvironment
    {
        IServiceProvider Services { get; }

        IServiceProvider CreateServiceProvider(Action<ServiceCollection> configure);
    }
}
