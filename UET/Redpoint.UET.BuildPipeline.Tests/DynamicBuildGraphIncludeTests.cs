namespace Redpoint.UET.BuildPipeline.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Redpoint.UET.BuildPipeline.BuildGraph.Dynamic;
    using Redpoint.UET.Configuration.Dynamic;
    using Redpoint.UET.Configuration.Plugin;
    using Redpoint.UET.BuildPipeline.Providers.Test;
    using Redpoint.UET.BuildPipeline.Providers.Deployment;
    using System.Text;
    using System.Threading.Tasks;

    public class DynamicBuildGraphIncludeTests
    {
        [Fact]
        public async Task TestPluginTests()
        {
            var services = new ServiceCollection();
            services.AddUETBuildPipelineProvidersTest();
            services.AddUETBuildPipelineProvidersDeployment();
            services.AddUETBuildPipeline();

            var sp = services.BuildServiceProvider();
            var writer = sp.GetRequiredService<IDynamicBuildGraphIncludeWriter>();

            using (var memory = new MemoryStream())
            {
                await writer.WriteBuildGraphInclude(
                    memory,
                    false,
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
                                    ConfigFiles = new string[0],
                                    MinWorkerCount = 16,
                                    Platforms = new BuildConfigHostPlatform[] { BuildConfigHostPlatform.Win64, BuildConfigHostPlatform.Mac },
                                    TestRunTimeoutMinutes = null,
                                }
                            }
                        }
                    });

                var result = Encoding.UTF8.GetString(memory.ToArray());
                Assert.Contains("TEST_A", result);
                Assert.Contains("A_PREFIX", result);
            }
        }
    }
}
