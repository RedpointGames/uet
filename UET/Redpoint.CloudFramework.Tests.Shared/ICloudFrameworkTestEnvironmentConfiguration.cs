namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.DependencyInjection;

    public interface ICloudFrameworkTestEnvironmentConfiguration
    {
        void RegisterServices(IServiceCollection services);
    }
}
