using Redpoint.UET.Automation;
using System.CommandLine;
using System.Diagnostics;

public static class AutomationProgram
{
    public static async Task<int> AutomationMain(string[] args)
    {
        var enginePathOpt = new Option<DirectoryInfo>(
            name: "--engine-path",
            description: "The path to the Unreal Engine install.")
        {
            IsRequired = true,
        };
        var projectRootOpt = new Option<DirectoryInfo>(
            name: "--project-root",
            description: "The directory which contains the project to test.")
        {
            IsRequired = true,
        };
        var projectPathOpt = new Option<FileInfo>(
            name: "--project-file",
            description: "The full path to the .uproject file.")
        {
            IsRequired = true,
        };
        var testPrefixOpt = new Option<string>(
            name: "--test-prefix",
            description: "The prefix to use when selecting the tests to run.")
        {
            IsRequired = true,
        };
        var workerCountOpt = new Option<int>(
            name: "--worker-count",
            description: "The number of automation workers. The default runs workers based on available memory and CPU cores.");
        var minWorkerCountOpt = new Option<int>(
            name: "--min-worker-count",
            description: "The minimum number of workers to run, regardless of memory and CPU core count.");
        var maxWorkerCountOpt = new Option<int>(
            name: "--max-worker-count",
            description: "The maximum number of workers to run, regardless of memory and CPU core count.");
        var timeoutMinutesOpt = new Option<int>(
            name: "--timeout-minutes",
            description: "The number of minutes after which the test should timeout. Default to 5 minutes.",
            getDefaultValue: () => 5);
        var executionAttemptsOpt = new Option<int>(
            name: "--execution-attempts",
            description: "The total number of times we will attempt to make tests pass, by retrying them if necessary. For example, if all but one test passes on attempt 1, we will try to re-run the failing test on attempt 2 in case the failure was intermittent, as long as --execution-attempts was 2 or greater.",
            getDefaultValue: () => 3);
        var displayStartingMessagesOpt = new Option<bool>(
            name: "--display-starting-messages",
            description: "If true, displays the starting log messages.",
            getDefaultValue: () => false);
        var displayFullLogsOpt = new Option<bool>(
            name: "--display-full-logs",
            description: "If true, displays all logs from all workers (which makes it hard to read the test results).");
        var junitExportOpt = new Option<FileInfo?>(
            name: "--junit-export",
            description: "If set, the test results will be written to this path in JUnit format (for GitLab).");
        var projectNameOpt = new Option<string>(
            name: "--project-name",
            description: "Used for JUnit export processing. Should make the name of the .uproject file without the extension.");
        var filenamePrefixToCutOpt = new Option<string>(
            name: "--filename-prefix-to-cut",
            description: "Used for JUnit export processing. The filename prefix to cut from the reported paths, to be re-anchored in the test result data so that filenames aren't specific to the build's working directory.");
        var simulateIoOpt = new Option<bool>(
            name: "--simulate-io",
            description: "If true, emits the content that would be submitted to an Io build server, even when the IO_URL and CI_JOB_JWT_V1 environment variables aren't set.");

        var rootCommand = new RootCommand("Automation runner for Unreal Engine");
        rootCommand.AddOption(enginePathOpt);
        rootCommand.AddOption(projectRootOpt);
        rootCommand.AddOption(projectPathOpt);
        rootCommand.AddOption(testPrefixOpt);
        rootCommand.AddOption(workerCountOpt);
        rootCommand.AddOption(minWorkerCountOpt);
        rootCommand.AddOption(maxWorkerCountOpt);
        rootCommand.AddOption(timeoutMinutesOpt);
        rootCommand.AddOption(executionAttemptsOpt);
        rootCommand.AddOption(displayStartingMessagesOpt);
        rootCommand.AddOption(displayFullLogsOpt);
        rootCommand.AddOption(junitExportOpt);
        rootCommand.AddOption(projectNameOpt);
        rootCommand.AddOption(filenamePrefixToCutOpt);
        rootCommand.AddOption(simulateIoOpt);

        rootCommand.SetHandler(async (context) =>
        {
            var enginePath = context.ParseResult.GetValueForOption(enginePathOpt);
            var projectRoot = context.ParseResult.GetValueForOption(projectRootOpt);
            var projectPath = context.ParseResult.GetValueForOption(projectPathOpt);
            var testPrefix = context.ParseResult.GetValueForOption(testPrefixOpt);
            var workerCount = context.ParseResult.GetValueForOption(workerCountOpt);
            var minWorkerCount = context.ParseResult.GetValueForOption(minWorkerCountOpt);
            var maxWorkerCount = context.ParseResult.GetValueForOption(maxWorkerCountOpt);
            var timeoutMinutes = context.ParseResult.GetValueForOption(timeoutMinutesOpt);
            var executionAttempts = context.ParseResult.GetValueForOption(executionAttemptsOpt);
            var displayStartingMessages = context.ParseResult.GetValueForOption(displayStartingMessagesOpt);
            var displayFullLogs = context.ParseResult.GetValueForOption(displayFullLogsOpt);
            var junitExport = context.ParseResult.GetValueForOption(junitExportOpt);
            var projectName = context.ParseResult.GetValueForOption(projectNameOpt);
            var filenamePrefixToCut = context.ParseResult.GetValueForOption(filenamePrefixToCutOpt);
            var simulateIo = context.ParseResult.GetValueForOption(simulateIoOpt);

            var logger = new ConsoleTestLogger(
                displayStartingMessages,
                displayFullLogs);

            if (!(enginePath?.Exists ?? false))
            {
                logger.LogError(null, $"The specified engine path does not exist: {enginePath?.FullName}");
                context.ExitCode = 1;
                return;
            }
            if (!(projectRoot?.Exists ?? false))
            {
                logger.LogError(null, $"The specified project root does not exist: {projectRoot?.FullName}");
                context.ExitCode = 1;
                return;
            }
            if (!(projectPath?.Exists ?? false))
            {
                logger.LogError(null, $"The specified project path does not exist: {projectPath?.FullName}");
                context.ExitCode = 1;
                return;
            }

            logger.LogStartup(null, $"--engine-path               = {enginePath}");
            logger.LogStartup(null, $"--project-root              = {projectRoot}");
            logger.LogStartup(null, $"--project-path              = {projectPath}");
            logger.LogStartup(null, $"--test-prefix               = {testPrefix}");
            logger.LogStartup(null, $"--worker-count              = {workerCount}");
            logger.LogStartup(null, $"--min-worker-count          = {minWorkerCount}");
            logger.LogStartup(null, $"--max-worker-count          = {maxWorkerCount}");
            logger.LogStartup(null, $"--timeout-minutes           = {timeoutMinutes}");
            logger.LogStartup(null, $"--execution-attempts        = {executionAttempts}");
            logger.LogStartup(null, $"--display-starting-messages = {displayStartingMessages}");
            logger.LogStartup(null, $"--display-full-logs         = {displayFullLogs}");
            logger.LogStartup(null, $"--junit-export              = {junitExport}");
            logger.LogStartup(null, $"--project-name              = {projectName}");
            logger.LogStartup(null, $"--filename-prefix-to-cut    = {filenamePrefixToCut}");
            logger.LogStartup(null, $"--simulate-io               = {simulateIo}");

            long availableMemoryMb = 0, consumedMemoryMb = 0;
            bool isMemoryDependent = false;
            if (workerCount == 0)
            {
                minWorkerCount = Math.Max(1, minWorkerCount);

                var availableMemory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                var engineRequiredMemory = (16L * 1024 * 1024 * 1024);
                var consumedMemory = minWorkerCount * engineRequiredMemory;
                while (availableMemory > consumedMemory + engineRequiredMemory && workerCount < maxWorkerCount)
                {
                    workerCount++;
                    consumedMemory += engineRequiredMemory;
                }

                availableMemoryMb = (long)Math.Round(availableMemory / 1024 / 1024.0);
                consumedMemoryMb = (long)Math.Round(consumedMemory / 1024 / 1024.0);
                isMemoryDependent = true;
            }
            if (minWorkerCount != 0)
            {
                workerCount = Math.Max(minWorkerCount, workerCount);
            }
            if (maxWorkerCount != 0)
            {
                workerCount = Math.Min(maxWorkerCount, workerCount);
            }
            if (workerCount <= 0)
            {
                workerCount = 1;
            }
            if (isMemoryDependent)
            {
                logger.LogInformation(null, $"Using {workerCount} workers ({availableMemoryMb}MB available, {consumedMemoryMb}MB planned to use)");
            }
            else
            {
                logger.LogInformation(null, $"Using {workerCount} workers");
            }

            var stopwatch = Stopwatch.StartNew();

            var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(context.GetCancellationToken());
            cancellationTokenSource.CancelAfter(TimeSpan.FromMinutes(timeoutMinutes));

            ITestNotification? testNotification = null;
            if ((!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IO_URL")) &&
                 !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI_JOB_JWT_V1"))) ||
                 simulateIo)
            {
                testNotification = new IoTestNotification(logger, simulateIo);
            }

            var workerPool = new WorkerPool(
                logger,
                workerCount,
                executionAttempts,
                testPrefix!,
                enginePath!.FullName,
                projectRoot!.FullName,
                projectPath!.FullName,
                testNotification);
            try
            {
                var results = await workerPool.RunAsync(cancellationTokenSource.Token);

                if (junitExport != null)
                {
                    JUnitConverter.WriteTestResults(
                        projectName ?? string.Empty,
                        filenamePrefixToCut ?? string.Empty,
                        junitExport,
                        results);
                    logger.LogInformation(null, $"Wrote test results in JUnit format to: {junitExport.FullName}");
                }

                if (results.AnyFailures)
                {
                    logger.LogInformation(null, $"One or more automation tests either failed or did not run.");
                    context.ExitCode = 1;
                    return;
                }
                else
                {
                    logger.LogInformation(null, $"Automation tests passed.");
                    context.ExitCode = 0;
                    return;
                }
            }
            catch (TaskCanceledException)
            {
                // Ensure processes are terminated (in case this task cancellation
                // was from some nested task that isn't dependent on cancellationTokenSource).
                cancellationTokenSource.Cancel();

                logger.LogInformation(null, $"Automation tests timed out (took longer than {timeoutMinutes} minutes). Try increasing the number of automation workers to reduce the test run time.");
                context.ExitCode = 1;
                return;
            }
            catch (Exception ex)
            {
                // Ensure processes are terminated.
                cancellationTokenSource.Cancel();

                logger.LogException(null, ex, $"Unknown exception while executing tests");
                context.ExitCode = 1;
                return;
            }
            finally
            {
                logger.LogTrace(null, $"Total execution time within AutomationRunner took {stopwatch.Elapsed.TotalSeconds} seconds.");

                testNotification?.CancellationTokenSource.Cancel();
                testNotification?.WaitAsync();
            }
        });

        return await rootCommand.InvokeAsync(args);
    }
}