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

        IWebAppConfigurator UseDevelopmentDockerContainers(Func<IConfiguration, string, DevelopmentDockerContainer[]> factory);

        IWebAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig);

        [Obsolete("This is ignored by OpenTelemetry tracing.")]
        IWebAppConfigurator FilterPathPrefixesFromSentryPerformance(string[] prefixes);

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
