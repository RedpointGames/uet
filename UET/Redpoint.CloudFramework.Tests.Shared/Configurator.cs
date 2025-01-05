namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Redpoint.CloudFramework.Startup;

    internal class Configurator : BaseConfigurator<CloudFrameworkTestEnvironment>
    {
        public void Configure(IHostEnvironment hostEnvironment, IServiceCollection services)
        {
            PreStartupConfigureServices(hostEnvironment, services);
            PostStartupConfigureServices(services);
        }
    }
}
