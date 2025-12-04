namespace Redpoint.CloudFramework.Startup
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.CloudFramework.Processor;
    using System;
    using System.Threading.Tasks;
    using System.Diagnostics.CodeAnalysis;

    public interface IServiceAppConfigurator : IBaseConfigurator<IServiceAppConfigurator>
    {
        [RequiresDynamicCode("This internally uses HostBuilder, which requires dynamic code.")]
        Task<int> StartServiceApp(string[] args);

        IServiceAppConfigurator AddContinuousProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IContinuousProcessor;

        IServiceAppConfigurator AddScheduledProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>() where T : class, IScheduledProcessor;

        IServiceAppConfigurator UseServiceConfiguration(Action<IServiceCollection> configureServices);

        IServiceAppConfigurator UseDevelopmentDockerContainers(Func<IConfiguration, string, DevelopmentDockerContainer[]> factory);

        IServiceAppConfigurator UseHelm(Func<IConfiguration, string, HelmConfiguration> helmConfig);

        IServiceAppConfigurator UseDefaultRoles(params string[] roleNames);
    }
}
