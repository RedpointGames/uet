namespace Redpoint.Uet.Configuration.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare;
    using Redpoint.Uet.BuildPipeline.Providers.Test;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.Configuration.Project;
    using System.Text.Json;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.Automation;
    using Redpoint.ProcessExecution;
    using Redpoint.PathResolution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace;
    using Redpoint.CredentialDiscovery;
    using Redpoint.Uet.Core;

    public class BuildConfigTests
    {
        private static ServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUetBuildPipelineProvidersPrepare();
            services.AddUETBuildPipelineProvidersTest();
            services.AddUETBuildPipelineProvidersDeployment();
            services.AddUETAutomation();
            services.AddUETWorkspace();
            services.AddUETCore(skipLoggingRegistration: true);
            services.AddReservation();
            services.AddCredentialDiscovery();
            return services;
        }

        [Fact]
        public void NestedProjectDistributionsWork()
        {
            var services = CreateServices();

            var sp = services.BuildServiceProvider();

            var basePath = Path.Combine("TestCases", "NestedProjectDistributions");
            using (var stream = new FileStream(Path.Combine(basePath, "BuildConfig.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buildConfig = JsonSerializer.Deserialize(stream, BuildConfigSourceGenerationContext.Create(sp, basePath).BuildConfig);
                var buildConfigProject = Assert.IsType<BuildConfigProject>(buildConfig);

                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "A");
                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "C");
                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "D");
                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "E");
            }
        }

        [Fact]
        public void PluginTests()
        {
            var services = CreateServices();

            var sp = services.BuildServiceProvider();

            var basePath = Path.Combine("TestCases", "PluginTests");
            using (var stream = new FileStream(Path.Combine(basePath, "BuildConfig.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buildConfig = JsonSerializer.Deserialize(stream, BuildConfigSourceGenerationContext.Create(sp, basePath).BuildConfig);
                var buildConfigPlugin = Assert.IsType<BuildConfigPlugin>(buildConfig);

                Assert.Single(buildConfigPlugin.Distributions);
                Assert.NotNull(buildConfigPlugin.Distributions[0].Tests);
                Assert.Single(buildConfigPlugin.Distributions[0].Tests!);
                Assert.Equal("Automation", buildConfigPlugin.Distributions[0].Tests![0].Type);
                Assert.Equal("AutomationTest", buildConfigPlugin.Distributions[0].Tests![0].Name);
                var automation = Assert.IsType<BuildConfigPluginTestAutomation>(buildConfigPlugin.Distributions[0].Tests![0].DynamicSettings);
                Assert.NotNull(automation);
                Assert.Equal("ABC", automation.TestPrefix);
                Assert.Equal(new[] { BuildConfigHostPlatform.Win64, BuildConfigHostPlatform.Mac }, automation.Platforms);
                Assert.Equal(new[] { "A", "B", }, automation.ConfigFiles);
                Assert.Equal(4, automation.MinWorkerCount);
                Assert.Null(automation.TestRunTimeoutMinutes);
            }
        }

        [Fact]
        public void PluginTestsWithExtraParameter()
        {
            var services = CreateServices();

            var sp = services.BuildServiceProvider();

            var basePath = Path.Combine("TestCases", "PluginTestsWithExtraParameter");
            using (var stream = new FileStream(Path.Combine(basePath, "BuildConfig.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var ex = Assert.Throws<JsonException>(() =>
                {
                    JsonSerializer.Deserialize(stream, BuildConfigSourceGenerationContext.Create(sp, basePath).BuildConfig);
                });
                Assert.Contains("Unexpected property 'Extra'", ex.Message);
            }
        }

        [Fact]
        public void PluginTestsWithNoProvider()
        {
            var services = CreateServices();

            var sp = services.BuildServiceProvider();

            var basePath = Path.Combine("TestCases", "PluginTestsWithNoProvider");
            using (var stream = new FileStream(Path.Combine(basePath, "BuildConfig.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var ex = Assert.Throws<JsonException>(() =>
                {
                    JsonSerializer.Deserialize(stream, BuildConfigSourceGenerationContext.Create(sp, basePath).BuildConfig);
                });
                Assert.Contains("Test of type 'Undefined' is not recognised as a test provider.", ex.Message);
            }
        }
    }
}