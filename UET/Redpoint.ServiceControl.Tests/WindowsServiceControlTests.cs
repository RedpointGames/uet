namespace Redpoint.ServiceControl.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    public class WindowsServiceControlTests
    {
        [SupportedOSPlatform("windows")]
        [Fact]
        public async Task TestServiceStatus()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test can only run on Windows.");

            var services = new ServiceCollection();
            services.AddServiceControl();
            var sp = services.BuildServiceProvider();
            var serviceControl = sp.GetRequiredService<IServiceControl>();

            Assert.True(
                await serviceControl.IsServiceRunning("EventLog", TestContext.Current.CancellationToken).ConfigureAwait(true),
                "Expected 'EventLog' service to be running.");
            Assert.False(
                await serviceControl.IsServiceRunning("SDRSVC", TestContext.Current.CancellationToken).ConfigureAwait(true),
                "Expected 'SDRSVC' (Windows Backup) service to not be running.");
        }
    }
}