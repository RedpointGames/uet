namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Server.Kestrel.Core;
    using Microsoft.Extensions.Configuration;
    using Redpoint.CloudFramework.Abstractions;
    using Redpoint.CloudFramework.Processor;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    public interface IWebAppConfigurator : IBaseConfigurator<IWebAppConfigurator>
    {
        IWebAppConfigurator UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] T>();

        [Obsolete("Automatically starting development Docker containers is no longer supported. Please use Helm and Rancher Desktop instead.", true)]
        IWebAppConfigurator UseDevelopmentDockerContainers(Func<IConfiguration, string, DevelopmentDockerContainer[]> factory);

        IWebAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig);

        IWebAppConfigurator UseHttp2Only(bool http2Only);

        IWebAppConfigurator UseKestrelOptions(Action<KestrelServerOptions> configure);

        Task<ICloudFrameworkWebHost> GetWebApp();

        Task StartWebApp();

        Task StartWebApp<T>() where T : IWebAppProvider;

        Task StartWebApp(ICloudFrameworkWebHost host);

        IWebAppConfigurator AddContinuousProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IContinuousProcessor;

        IWebAppConfigurator AddScheduledProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IScheduledProcessor;
    }
}
