namespace Redpoint.ServiceControl.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    public class MacServiceControlTests
    {
        [SupportedOSPlatform("macos")]
        [Fact(Skip = "We know this works, but we don't have a reliably running macOS service to test status against.")]
        public async Task TestServiceStatus()
        {
            Assert.SkipUnless(OperatingSystem.IsMacOS(), "This test only runs on macOS.");

            var services = new ServiceCollection();
            services.AddServiceControl();
            var sp = services.BuildServiceProvider();
            var serviceControl = sp.GetRequiredService<IServiceControl>();

            Assert.True(
                await serviceControl.IsServiceRunning("com.apple.lskdd", TestContext.Current.CancellationToken).ConfigureAwait(true),
                "Expected 'com.apple.lskdd' to be running.");
            Assert.False(
                await serviceControl.IsServiceRunning("com.apple.afpfs_afpLoad", TestContext.Current.CancellationToken).ConfigureAwait(true),
                "Expected 'com.apple.afpfs_afpLoad' to not be running.");
        }
    }
}