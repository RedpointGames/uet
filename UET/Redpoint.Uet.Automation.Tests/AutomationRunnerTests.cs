namespace Redpoint.Uet.Automation.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Automation.Runner;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.TestNotification;
    using Redpoint.Uet.Automation.TestReporter;
    using Redpoint.Uet.Automation.Worker;
    using Redpoint.Uet.Uat;

    public class AutomationRunnerTests
    {
        private readonly ITestOutputHelper _output;

        public AutomationRunnerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task CanRunAutomationTests()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test only runs on Windows.");
            Assert.SkipUnless(Directory.Exists(@"E:\EpicGames\UE_5.2"), "Expected path does not exist.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal"), "Expected path does not exist.");

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.SetMinimumLevel(LogLevel.Trace);
                builder.AddXUnit(
                    _output,
                    configure =>
                    {
                        configure.Filter = (group, category) =>
                        {
                            /*
                            if (group!.Contains("LocalEditorWorker"))
                            {
                                return false;
                            }
                            */
                            return true;
                        };
                    });
            });
            services.AddProcessExecution();
            services.AddUETAutomation();
            services.AddUETUAT();
            services.AddReservation();

            var sp = services.BuildServiceProvider();

            var logger = sp.GetRequiredService<ITestLoggerFactory>().CreateConsole();
            var notification = sp.GetRequiredService<ITestNotificationFactory>().CreateNull();
            var reporter = sp.GetRequiredService<ITestReporterFactory>().CreateNull();

            var factory = sp.GetRequiredService<IAutomationRunnerFactory>();

            await using (var automationRunner = await factory.CreateAndRunAsync(
                logger,
                notification,
                reporter,
                new[]
                {
                    new DesiredWorkerDescriptor
                    {
                        Platform = "Win64",
                        IsEditor = true,
                        Configuration = "Development",
                        Target = "ExampleOSSEditor",
                        UProjectPath = @"C:\Work\internal\EOS_OSB\EOS_OSB\ExampleOSS.uproject",
                        EnginePath = @"E:\EpicGames\UE_5.2",
                        MinWorkerCount = 1,
                        MaxWorkerCount = null,
                        EnableRendering = true,
                    }
                },
                new AutomationRunnerConfiguration
                {
                    ProjectName = "ExampleOSS",
                    TestPrefix = "OnlineSubsystemEOS.OnlineIdentityInterface.",
                    TestRunTimeout = TimeSpan.FromSeconds(1200),
                    FilenamePrefixToCut = @"C:\Work\internal\EOS_OSB\EOS_OSB",
                },
                CancellationToken.None))
            {
                var results = await automationRunner.WaitForResultsAsync();
                Assert.True(results.Length > 0, "Expected at least one test result");
            }
        }
    }
}