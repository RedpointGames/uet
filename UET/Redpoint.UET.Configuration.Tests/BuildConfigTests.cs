namespace Redpoint.UET.Configuration.Tests
{
    using Redpoint.UET.Configuration.Project;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    public class BuildConfigTests
    {
        [Fact]
        public void NestedProjectDistributionsWork()
        {
            var basePath = "TestCases\\NestedProjectDistributions";
            using (var stream = new FileStream(Path.Combine(basePath, "BuildConfig.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var buildConfig = JsonSerializer.Deserialize(stream, BuildConfigSourceGenerationContext.WithDynamicBuildConfig(basePath).BuildConfig);
                var buildConfigProject = Assert.IsType<BuildConfigProject>(buildConfig);

                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "A");
                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "C");
                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "D");
                Assert.Contains(buildConfigProject.Distributions, x => x.Name == "E");
            }
        }
    }
}