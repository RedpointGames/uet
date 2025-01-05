namespace Redpoint.CloudFramework.Tests
{
    using System;

    public interface ICloudFrameworkTestEnvironment
    {
        IServiceProvider Services { get; }
    }
}
