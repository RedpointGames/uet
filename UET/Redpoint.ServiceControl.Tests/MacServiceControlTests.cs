namespace Redpoint.ServiceControl.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;

    public class MacServiceControlTests
    {
        [SupportedOSPlatform("macos")]
        [SkippableFact]
        public async void TestServiceStatus()
        {
            Skip.IfNot(OperatingSystem.IsMacOS());

            var services = new ServiceCollection();
            services.AddServiceControl();
            var sp = services.BuildServiceProvider();
            var serviceControl = sp.GetRequiredService<IServiceControl>();

            Assert.True(
                await serviceControl.IsServiceRunning("com.apple.lskdd"),
                "Expected 'com.apple.lskdd' to be running.");
            Assert.False(
                await serviceControl.IsServiceRunning("com.apple.afpfs_afpLoad"),
                "Expected 'com.apple.afpfs_afpLoad' to not be running.");
        }
    }
}