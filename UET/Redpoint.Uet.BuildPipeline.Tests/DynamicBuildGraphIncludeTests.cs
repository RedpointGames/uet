namespace Redpoint.Uet.BuildPipeline.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.Uet.BuildPipeline.BuildGraph.Dynamic;
    using Redpoint.Uet.Configuration.Dynamic;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.BuildPipeline.Providers.Prepare;
    using Redpoint.Uet.BuildPipeline.Providers.Test;
    using Redpoint.Uet.BuildPipeline.Providers.Deployment;
    using System.Text;
    using System.Threading.Tasks;
    using Redpoint.Uet.Configuration;
    using Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation;
    using Redpoint.Uet.Automation;
    using Redpoint.ProcessExecution;
    using Redpoint.PathResolution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Workspace;
    using Redpoint.CredentialDiscovery;
    using Redpoint.Uet.Core;
    using Redpoint.PackageManagement;
    using Redpoint.ProgressMonitor;

    public class DynamicBuildGraphIncludeTests
    {
        private static ServiceCollection CreateServices()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddPathResolution();
            services.AddProcessExecution();
            services.AddUETBuildPipeline();
            services.AddUetBuildPipelineProvidersPrepare();
            services.AddUETBuildPipelineProvidersTest();
            services.AddUETBuildPipelineProvidersDeployment();
            services.AddUETAutomation();
            services.AddUetWorkspace();
            services.AddPackageManagement();
            services.AddProgressMonitor();
            services.AddReservation();
            services.AddCredentialDiscovery();
            services.AddUETCore(skipLoggingRegistration: true);
            return services;
        }

        [Fact]
        public async Task TestPluginTests()
        {
            var services = CreateServices();

            var sp = services.BuildServiceProvider();
            var writer = sp.GetRequiredService<IDynamicBuildGraphIncludeWriter>();

            using (var memory = new MemoryStream())
            {
                await writer.WriteBuildGraphNodeInclude(
                    memory,
                    false,
                    new BuildConfigPlugin(),
                    new BuildConfigPluginDistribution
                    {
                        Name = "Test",
                        Tests = new[]
                        {
                            new BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>
                            {
                                Name = "TEST_A",
                                Type = "Automation",
                                DynamicSettings = new BuildConfigPluginTestAutomation
                                {
                                    TestPrefix = "A_PREFIX",
                                    ConfigFiles = Array.Empty<string>(),
                                    MinWorkerCount = 16,
                                    Platforms = new BuildConfigHostPlatform[] { BuildConfigHostPlatform.Win64, BuildConfigHostPlatform.Mac },
                                    TestRunTimeoutMinutes = null,
                                }
                            }
                        }
                    },
                    true,
                    true);

                var result = Encoding.UTF8.GetString(memory.ToArray());
                Assert.Contains("TEST_A", result);
                Assert.Contains("A_PREFIX", result);
            }
        }
    }
}
