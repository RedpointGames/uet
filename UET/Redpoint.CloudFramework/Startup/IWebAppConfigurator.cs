namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Quartz;
    using Redpoint.CloudFramework.Abstractions;
    using Redpoint.CloudFramework.Processor;
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Threading.Tasks;

    public interface IWebAppConfigurator : IBaseConfigurator<IWebAppConfigurator>
    {
        IWebAppConfigurator UseStartup<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicMethods)] T>();

        IWebAppConfigurator UseSentryTracing(double tracingRate);

        [Obsolete("Call UseSentryTracing instead.")]
        IWebAppConfigurator UsePerformanceTracing(double tracingRate);

        IWebAppConfigurator UseDevelopmentDockerContainers(Func<IConfiguration, string, DevelopmentDockerContainer[]> factory);

        IWebAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig);

        IWebAppConfigurator FilterPathPrefixesFromSentryPerformance(string[] prefixes);

        Task<IWebHost> GetWebApp();

        Task StartWebApp();

        Task StartWebApp<T>() where T : IWebAppProvider;

        Task StartWebApp(IWebHost host);

        IWebAppConfigurator AddDevelopmentProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IContinuousProcessor;

        IWebAppConfigurator AddDevelopmentProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(Action<TriggerBuilder> triggerBuilder) where T : class, IScheduledProcessor;
    }
}
