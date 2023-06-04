namespace Redpoint.UET.BuildPipeline.Providers.Test
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.UET.BuildPipeline.Providers.Test.Plugin.Custom;
    using Redpoint.UET.BuildPipeline.Providers.Test.Plugin.Downstream;
    using Redpoint.UET.BuildPipeline.Providers.Test.Plugin.Gauntlet;
    using Redpoint.UET.BuildPipeline.Providers.Test.Project.Custom;
    using Redpoint.UET.BuildPipeline.Providers.Test.Project.Gauntlet;
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.Configuration.Project;

    public static class TestProviderServiceCollections
    {
        public static void AddUETBuildPipelineProvidersTest(this IServiceCollection services)
        {
            services.AddSingleton<IPluginTestProjectEmitProvider, DefaultPluginTestProjectEmitProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>, AutomationPluginTestProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>, CustomPluginTestProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>, GauntletPluginTestProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigPluginDistribution, ITestProvider>, DownstreamPluginTestProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>, CustomProjectTestProvider>();
            services.AddSingleton<IDynamicProvider<BuildConfigProjectDistribution, ITestProvider>, GauntletProjectTestProvider>();
        }
    }
}