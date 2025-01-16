namespace Redpoint.CloudFramework.Tests
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.FileProviders;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Primitives;
    using Redpoint.CloudFramework.Startup;
    using System.Collections.Generic;

    internal class Configurator : BaseConfigurator<CloudFrameworkTestEnvironment>
    {
        public void Configure(IHostEnvironment hostEnvironment, IServiceCollection services)
        {
            PreStartupConfigureServices(hostEnvironment, new ConfigurationBuilder().Build(), services);
            PostStartupConfigureServices(services);
        }
    }
}
