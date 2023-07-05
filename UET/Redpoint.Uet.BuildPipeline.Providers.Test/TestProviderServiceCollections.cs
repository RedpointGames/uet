namespace Redpoint.Uet.BuildPipeline.Providers.Test
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Commandlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Downstream;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Gauntlet;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Custom;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Gauntlet;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Project.Automation;

    public static class TestProviderServiceCollections
    {
        public static void AddUETBuildPipelineProvidersTest(this IServiceCollection services)
        {
            services.AddSingleton<IPluginTestProjectEmitProvider, DefaultPluginTestProjectEmitProvider>();
            services.AddDynamicProvider<BuildConfigPluginDistribution, ITestProvider, AutomationPluginTestProvider>();
            services.AddDynamicProvider<BuildConfigPluginDistribution, ITestProvider, CommandletPluginTestProvider>();
            services.AddDynamicProvider<BuildConfigPluginDistribution, ITestProvider, CustomPluginTestProvider>();
            services.AddDynamicProvider<BuildConfigPluginDistribution, ITestProvider, GauntletPluginTestProvider>();
            services.AddDynamicProvider<BuildConfigPluginDistribution, ITestProvider, DownstreamPluginTestProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, ITestProvider, AutomationProjectTestProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, ITestProvider, CustomProjectTestProvider>();
            services.AddDynamicProvider<BuildConfigProjectDistribution, ITestProvider, GauntletProjectTestProvider>();
        }
    }
}