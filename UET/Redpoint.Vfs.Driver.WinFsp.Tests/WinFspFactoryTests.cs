namespace Redpoint.Vfs.Driver.WinFsp.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using System.Runtime.Versioning;
    using Redpoint.Vfs.LocalIo;
    using Redpoint.Vfs.Layer.Scratch;
    using System.Security.Principal;

    public class WinFspFactoryTests
    {
        [SkippableFact]
        [SupportedOSPlatform("windows6.2")]
        public void CanConstructWinFspFromFactory()
        {
            Skip.IfNot(OperatingSystem.IsWindows(), "This test must be run on Windows");
            Skip.IfNot(new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator), "This test must be run as Administrator");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddScratchLayerFactory();
            services.AddLocalIoFileFactory();
            services.AddWinFspVfsDriver();

            var serviceProvider = services.BuildServiceProvider();

            var winfspFactory = serviceProvider.GetRequiredService<IVfsDriverFactory>();

            using (var winfsp = winfspFactory.InitializeAndMount(
                new TestVfsLayer(),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "WinFspFactoryTest"),
                new VfsDriverOptions
                {
                    DriverLogPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "WinFspFactoryTest.log"),
                }))
            {
            }
        }
    }
}