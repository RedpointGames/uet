namespace Redpoint.Logging.Mac.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Runtime.Versioning;

    public class MacLoggerTests
    {
        [SkippableFact]
        [SupportedOSPlatform("macos")]
        public void TestMacLogging()
        {
            Skip.IfNot(OperatingSystem.IsMacOS());

            var services = new ServiceCollection();
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddMac();
            });

            var sp = services.BuildServiceProvider();

            var logger = sp.GetRequiredService<ILogger<MacLoggerTests>>();
            logger.LogInformation("This is my test message.");
        }
    }
}