namespace Redpoint.ServiceControl.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    public class WindowsServiceControlTests
    {
        [SupportedOSPlatform("windows")]
        [SkippableFact]
        public async void TestServiceStatus()
        {
            Skip.IfNot(OperatingSystem.IsWindows());

            var services = new ServiceCollection();
            services.AddServiceControl();
            var sp = services.BuildServiceProvider();
            var serviceControl = sp.GetRequiredService<IServiceControl>();

            Assert.True(
                await serviceControl.IsServiceRunning("EventLog").ConfigureAwait(false),
                "Expected 'EventLog' service to be running.");
            Assert.False(
                await serviceControl.IsServiceRunning("SDRSVC").ConfigureAwait(false),
                "Expected 'SDRSVC' (Windows Backup) service to not be running.");
        }
    }
}