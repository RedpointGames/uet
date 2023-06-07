namespace Redpoint.UET.Automation.Tests
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.ProcessExecution;
    using Redpoint.PathResolution;
    using Redpoint.ProcessExecution;
    using Redpoint.UET.Automation.Runner;
    using Redpoint.UET.Automation.TestLogging;
    using Redpoint.UET.Automation.TestNotification;
    using Redpoint.UET.Automation.TestReporter;
    using Redpoint.UET.Automation.Worker;
    using Redpoint.UET.Core;
    using Redpoint.UET.UAT;
    using Xunit.Abstractions;

    public class AutomationRunnerTests
    {
        private readonly ITestOutputHelper _output;

        public AutomationRunnerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [SkippableFact]
        public async Task CanRunAutomationTests()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"E:\EpicGames\UE_5.2"));
            Skip.IfNot(Directory.Exists(@"C:\Work\internal"));

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
                    TestRunTimeout = TimeSpan.FromSeconds(120),
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