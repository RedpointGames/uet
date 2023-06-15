namespace Redpoint.ProcessTree.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    public class ProcessTreeTests
    {
        [Fact]
        [SupportedOSPlatform("windows")]
        [SupportedOSPlatform("macos")]
        [SupportedOSPlatform("linux")]
        public void CanGetParentProcess()
        {
            var services = new ServiceCollection();
            services.AddProcessTree();

            var sp = services.BuildServiceProvider();
            var processTree = sp.GetRequiredService<IProcessTree>();

            Assert.NotNull(processTree.GetParentProcess());
        }
    }
}