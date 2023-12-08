namespace Redpoint.Uet.BuildPipeline.Providers.Test.Plugin.Automation
{
    using Redpoint.Concurrency;
    using Redpoint.Uet.Configuration.Plugin;
    using Redpoint.Uet.BuildGraph;
    using System.Threading.Tasks;
    using System.Xml;
    using Redpoint.Uet.Configuration.Dynamic;
    using System.Text.Json;
    using System.Threading;
    using Redpoint.Uet.Automation.Runner;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Automation.TestNotification;
    using Redpoint.Uet.Automation.TestReporter;
    using Redpoint.Uet.Automation.Worker;
    using Redpoint.Uet.Automation.Model;
    using System.Text.Json.Serialization.Metadata;
    using System.Text.Json.Serialization;
    using Redpoint.RuntimeJson;
    using System.Globalization;

    internal sealed class AutomationPluginTestProvider : IPluginTestProvider, IDynamicReentrantExecutor<BuildConfigPluginDistribution, BuildConfigPluginTestAutomation>
    {
        private readonly IPluginTestProjectEmitProvider _pluginTestProjectEmitProvider;
        private readonly IAutomationRunnerFactory _automationRunnerFactory;
        private readonly ITestLoggerFactory _testLoggerFactory;
        private readonly ITestNotificationFactory _testNotificationFactory;
        private readonly ITestReporterFactory _testReporterFactory;

        public AutomationPluginTestProvider(
            IPluginTestProjectEmitProvider pluginTestProjectEmitProvider,
            IAutomationRunnerFactory automationRunnerFactory,
            ITestLoggerFactory testLoggerFactory,
            ITestNotificationFactory testNotificationFactory,
            ITestReporterFactory testReporterFactory)
        {
            _pluginTestProjectEmitProvider = pluginTestProjectEmitProvider;
            _automationRunnerFactory = automationRunnerFactory;
            _testLoggerFactory = testLoggerFactory;
            _testNotificationFactory = testNotificationFactory;
            _testReporterFactory = testReporterFactory;
        }

        public string Type => "Automation";

        public IRuntimeJson DynamicSettings { get; } = new TestProviderRuntimeJson(TestProviderSourceGenerationContext.WithStringEnum).BuildConfigPluginTestAutomation;

        public async Task WriteBuildGraphNodesAsync(
            IBuildGraphEmitContext context,
            XmlWriter writer,
            BuildConfigPluginDistribution buildConfigDistribution,
            IEnumerable<BuildConfigDynamic<BuildConfigPluginDistribution, ITestProvider>> dynamicSettings)
        {
            var castedSettings = dynamicSettings
                .Select(x => (name: x.Name, settings: (BuildConfigPluginTestAutomation)x.DynamicSettings))
                .ToList();

            // Ensure we have the test project available.
            await _pluginTestProjectEmitProvider.EnsureTestProjectNodesArePresentAsync(
                context,
                writer).ConfigureAwait(false);

            // Emit the nodes to run each test.
            var allPlatforms = castedSettings.SelectMany(x => x.settings.Platforms).Where(context.CanHostPlatformBeUsed).ToHashSet();
            foreach (var platform in allPlatforms)
            {
                await writer.WriteAgentAsync(
                    new AgentElementProperties
                    {
                        Name = $"Automation {platform} Tests",
                        Type = platform.ToString()
                    },
                    async writer =>
                    {
                        foreach (var test in castedSettings)
                        {
                            if (!test.settings.Platforms.Contains(platform))
                            {
                                continue;
                            }

                            var nodeName = $"Automation {platform} {test.name}";

                            await writer.WriteNodeAsync(
                                new NodeElementProperties
                                {
                                    Name = nodeName,
                                    Requires = _pluginTestProjectEmitProvider.GetTestProjectTags(platform),
                                    If = $"'$(CanBuildEditor{platform})' == 'true'"
                                },
                                async writer =>
                                {
                                    foreach (var configFile in test.settings.ConfigFiles ?? Array.Empty<string>())
                                    {
                                        await writer.WriteCopyAsync(
                                            new CopyElementProperties
                                            {
                                                Files = "...",
                                                From = $"$(ProjectRoot)/{configFile}/",
                                                To = $"{_pluginTestProjectEmitProvider.GetTestProjectDirectoryPath(platform)}/Config/",
                                            }).ConfigureAwait(false);
                                    }


                                    var arguments = new List<string>();
                                    if (test.settings.MinWorkerCount != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--min-worker-count",
                                            test.settings.MinWorkerCount.Value.ToString(CultureInfo.InvariantCulture)
                                        });
                                    }
                                    if (test.settings.TestRunTimeoutMinutes != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--test-run-timeout-minutes",
                                            test.settings.TestRunTimeoutMinutes.Value.ToString(CultureInfo.InvariantCulture)
                                        });
                                    }
                                    if (test.settings.TestTimeoutMinutes != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--test-timeout-minutes",
                                            test.settings.TestTimeoutMinutes.Value.ToString(CultureInfo.InvariantCulture)
                                        });
                                    }
                                    if (test.settings.TestAttemptCount != null)
                                    {
                                        arguments.AddRange(new string[]
                                        {
                                            "--test-attempt-count",
                                            test.settings.TestAttemptCount.Value.ToString(CultureInfo.InvariantCulture)
                                        });
                                    }

                                    await writer.WriteDynamicReentrantSpawnAsync<
                                        AutomationPluginTestProvider,
                                        BuildConfigPluginDistribution,
                                        BuildConfigPluginTestAutomation>(
                                        this,
                                        context,
                                        $"{platform}.{test.name}".Replace(" ", ".", StringComparison.Ordinal),
                                        test.settings,
                                        new Dictionary<string, string>
                                        {
                                            { "EnginePath", "$(EnginePath)" },
                                            { "TestProjectPath", _pluginTestProjectEmitProvider.GetTestProjectUProjectFilePath(platform) },
                                            { "TestResultsPath", $"$(ArtifactExportPath)/.uet/tmp/Automation{platform}/TestResults.xml" },
                                            { "WorkerLogsPath", $"$(ArtifactExportPath)/.uet/tmp/Automation{platform}" },
                                        }).ConfigureAwait(false);
                                }).ConfigureAwait(false);
                            await writer.WriteDynamicNodeAppendAsync(
                                new DynamicNodeAppendElementProperties
                                {
                                    NodeName = nodeName,
                                    MustPassForLaterDeployment = true,
                                }).ConfigureAwait(false);
                        }
                    }).ConfigureAwait(false);
            }
        }

        public async Task<int> ExecuteBuildGraphNodeAsync(
            object configUnknown,
            Dictionary<string, string> runtimeSettings,
            CancellationToken cancellationToken)
        {
            var config = (BuildConfigPluginTestAutomation)configUnknown;

            var enginePath = runtimeSettings["EnginePath"];
            var testProjectPath = runtimeSettings["TestProjectPath"];
            var testResultsPath = runtimeSettings["TestResultsPath"];
            var workerLogsPath = runtimeSettings["WorkerLogsPath"];

            await using ((await _automationRunnerFactory.CreateAndRunAsync(
                _testLoggerFactory.CreateConsole(),
                _testNotificationFactory.CreateIo(cancellationToken),
                _testReporterFactory.CreateJunit(testResultsPath),
                new[]
                {
                    new DesiredWorkerDescriptor
                    {
                        Platform = OperatingSystem.IsWindows() ? "Win64" : "Mac",
                        IsEditor = true,
                        Configuration = "Development",
                        Target = "UnrealEditor",
                        UProjectPath = testProjectPath,
                        EnginePath = enginePath,
                        MinWorkerCount = config.MinWorkerCount,
                        MaxWorkerCount = null,
                        EnableRendering = false,
                        WorkerLogsPath = workerLogsPath,
                    }
                },
                new AutomationRunnerConfiguration
                {
                    ProjectName = Path.GetFileNameWithoutExtension(testProjectPath)!,
                    TestPrefix = config.TestPrefix,
                    TestTimeout = config.TestTimeoutMinutes.HasValue ? TimeSpan.FromMinutes(config.TestTimeoutMinutes.Value) : null,
                    TestRunTimeout = config.TestRunTimeoutMinutes.HasValue ? TimeSpan.FromMinutes(config.TestRunTimeoutMinutes.Value) : TimeSpan.FromMinutes(5),
                    TestAttemptCount = config.TestAttemptCount.HasValue ? Math.Max(config.TestAttemptCount.Value, 1) : null,
                    FilenamePrefixToCut = Path.GetDirectoryName(testProjectPath)!,
                },
                cancellationToken).ConfigureAwait(false)).AsAsyncDisposable(out var automationRunner).ConfigureAwait(false))
            {
                var testResults = await automationRunner.WaitForResultsAsync().ConfigureAwait(false);
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