namespace Redpoint.Logging.Mac.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using System.Runtime.Versioning;

    public class MacLoggerTests
    {
        [Fact]
        [SupportedOSPlatform("macos")]
        public void TestMacLogging()
        {
            Assert.SkipUnless(OperatingSystem.IsMacOS(), "This test only runs on macOS.");

            var services = new ServiceCollection();
            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddMac();
            });

            var sp = services.BuildServiceProvider();

            var logger = sp.GetRequiredService<ILogger<MacLoggerTests>>();
            logger.LogError("This is my test message.");
            logger.LogWarning("This is my test message.");
            logger.LogInformation("This is my test message.");
            logger.LogDebug("This is my test message.");
            logger.LogTrace("This is my test message.");
        }
    }
}