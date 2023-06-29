namespace UET.Commands.Internal.RunAutomationTestFromBuildGraph
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uet.Automation.Model;
    using Redpoint.Uet.Automation.Runner;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.TestNotification;
    using Redpoint.Uet.Automation.TestReporter;
    using Redpoint.Uet.Automation.Worker;
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.Linq;
    using System.Threading.Tasks;

    internal class RunAutomationTestFromBuildGraphCommand
    {
        internal class Options
        {
            public Option<string> EnginePath;
            public Option<string> TestProjectPath;
            public Option<string> TestPrefix;
            public Option<int?> MinWorkerCount;
            public Option<int?> TestTimeoutMinutes;
            public Option<int?> TestRunTimeoutMinutes;
            public Option<int?> TestAttemptCount;
            public Option<string> TestResultsPath;

            public Options()
            {
                EnginePath = new Option<string>("--engine-path") { IsRequired = true };
                TestProjectPath = new Option<string>("--test-project-path") { IsRequired = true };
                TestPrefix = new Option<string>("--test-prefix") { IsRequired = true };
                MinWorkerCount = new Option<int?>("--min-worker-count");
                TestTimeoutMinutes = new Option<int?>("--test-timeout-minutes");
                TestRunTimeoutMinutes = new Option<int?>("--test-run-timeout-minutes");
                TestAttemptCount = new Option<int?>("--test-attempt-count");
                TestResultsPath = new Option<string>("--test-results-path");
            }
        }

        public static Command CreateRunAutomationTestFromBuildGraphCommand()
        {
            var options = new Options();
            var command = new Command("run-automation-test-from-buildgraph");
            command.AddAllOptions(options);
            command.AddCommonHandler<RunAutomationTestFromBuildGraphCommandInstance>(options);
            return command;
        }

        private class RunAutomationTestFromBuildGraphCommandInstance : ICommandInstance
        {
            private readonly ILogger<RunAutomationTestFromBuildGraphCommandInstance> _logger;
            private readonly Options _options;
            private readonly IAutomationRunnerFactory _automationRunnerFactory;
            private readonly ITestLoggerFactory _testLoggerFactory;
            private readonly ITestNotificationFactory _testNotificationFactory;
            private readonly ITestReporterFactory _testReporterFactory;

            public RunAutomationTestFromBuildGraphCommandInstance(
                ILogger<RunAutomationTestFromBuildGraphCommandInstance> logger,
                Options options,
                IAutomationRunnerFactory automationRunnerFactory,
                ITestLoggerFactory testLoggerFactory,
                ITestNotificationFactory testNotificationFactory,
                ITestReporterFactory testReporterFactory)
            {
                _logger = logger;
                _options = options;
                _automationRunnerFactory = automationRunnerFactory;
                _testLoggerFactory = testLoggerFactory;
                _testNotificationFactory = testNotificationFactory;
                _testReporterFactory = testReporterFactory;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var enginePath = context.ParseResult.GetValueForOption(_options.EnginePath);
                var testProjectPath = context.ParseResult.GetValueForOption(_options.TestProjectPath);
                var testPrefix = context.ParseResult.GetValueForOption(_options.TestPrefix);
                var minWorkerCount = context.ParseResult.GetValueForOption(_options.MinWorkerCount);
                var testTimeoutMinutes = context.ParseResult.GetValueForOption(_options.TestTimeoutMinutes);
                var testRunTimeoutMinutes = context.ParseResult.GetValueForOption(_options.TestRunTimeoutMinutes);
                var testAttemptCount = context.ParseResult.GetValueForOption(_options.TestAttemptCount);
                var testResultsPath = context.ParseResult.GetValueForOption(_options.TestResultsPath);

                await using (var automationRunner = await _automationRunnerFactory.CreateAndRunAsync(
                    _testLoggerFactory.CreateConsole(),
                    _testNotificationFactory.CreateIo(context.GetCancellationToken()),
                    testResultsPath == null ? _testReporterFactory.CreateNull() : _testReporterFactory.CreateJunit(testResultsPath),
                    new[]
                    {
                        new DesiredWorkerDescriptor
                        {
                            Platform = OperatingSystem.IsWindows() ? "Win64" : "Mac",
                            IsEditor = true,
                            Configuration = "Development",
                            Target = "UnrealEditor",
                            UProjectPath = testProjectPath!,
                            EnginePath = enginePath!,
                            MinWorkerCount = minWorkerCount,
                            MaxWorkerCount = null,
                            EnableRendering = false,
                        }
                    },
                    new AutomationRunnerConfiguration
                    {
                        ProjectName = Path.GetFileNameWithoutExtension(testProjectPath)!,
                        TestPrefix = testPrefix!,
                        TestTimeout = testTimeoutMinutes.HasValue ? TimeSpan.FromMinutes(testTimeoutMinutes.Value) : null,
                        TestRunTimeout = testRunTimeoutMinutes.HasValue ? TimeSpan.FromMinutes(testRunTimeoutMinutes.Value) : TimeSpan.FromMinutes(5),
                        TestAttemptCount = testAttemptCount.HasValue ? Math.Max(testAttemptCount.Value, 1) : null,
                        FilenamePrefixToCut = Path.GetDirectoryName(testProjectPath)!,
                    },
                    context.GetCancellationToken()))
                {
                    var testResults = await automationRunner.WaitForResultsAsync();
                    if (testResults.Length == 0 ||
                        testResults.Any(x => x.TestStatus != TestResultStatus.Passed && x.TestStatus != TestResultStatus.Skipped))
                    {
                        return 1;
                    }
                    return 0;
                }
            }
        }
    }
}
