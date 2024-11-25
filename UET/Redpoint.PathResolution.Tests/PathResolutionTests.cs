namespace Redpoint.PathResolution.Tests
{
    using System.Runtime.Versioning;

    public class PathResolutionTests
    {
        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanResolveCmd()
        {
            if (OperatingSystem.IsWindows())
            {
                var expectedPath = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "cmd.exe");
                var resolvedPath = await new DefaultPathResolver().ResolveBinaryPath("cmd").ConfigureAwait(true);
                Assert.Equal(expectedPath, resolvedPath, ignoreCase: true);
            }
            else
            {
                Assert.True(true);
            }
        }

        [Fact]
        [SupportedOSPlatform("windows")]
        public async Task CanResolvePowerShell()
        {
            if (OperatingSystem.IsWindows())
            {
                var expectedPath = Path.Combine(Environment.GetEnvironmentVariable("SYSTEMROOT")!, "system32", "WindowsPowerShell", "v1.0", "powershell.exe");
                var resolvedPath = await new DefaultPathResolver().ResolveBinaryPath("powershell").ConfigureAwait(true);
                Assert.Equal(expectedPath, resolvedPath, ignoreCase: true);
            }
            else
            {
                Assert.True(true);
            }
        }
    }
}