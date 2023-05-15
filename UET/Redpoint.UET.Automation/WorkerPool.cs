namespace Redpoint.UET.Automation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    public class WorkerPool : ICrashHooks
    {
        private readonly ITestLogger _testLogger;
        private readonly int _totalAttemptCount;
        private readonly ITestNotification? _testNotification;
        private readonly string _sessionGuid;
        private readonly ControllerWorker _controllerWorker;
        private readonly List<Worker> _additionalWorkers;
        private JsonResultMonitor<int>? _monitor;

        public WorkerPool(
            ITestLogger testLogger,
            int workerCount,
            int totalAttemptCount,
            string testPrefix,
            string enginePath,
            string projectRoot,
            string projectPath,
            ITestNotification? testNotification)
        {
            _testLogger = testLogger;
            _totalAttemptCount = totalAttemptCount;
            _testNotification = testNotification;

            _sessionGuid = Guid.NewGuid().ToString();
            var sessionName = "Automation";
            var ciJobId = Environment.GetEnvironmentVariable("CI_JOB_ID");
            if (!string.IsNullOrWhiteSpace(ciJobId))
            {
                sessionName = $"Automation{ciJobId}";
            }

            _controllerWorker = new ControllerWorker(
                _testLogger,
                _sessionGuid,
                sessionName,
                1,
                enginePath,
                projectRoot,
                projectPath,
                this)
            {
                TestPrefix = testPrefix,
            };
            _additionalWorkers = Enumerable.Range(2, workerCount - 1).Select(i => new Worker(
                _testLogger,
                _sessionGuid,
                sessionName,
                i,
                enginePath,
                projectRoot,
                projectPath,
                this)).ToList();
        }

        public void OnWorkerCrashing(Worker worker)
        {
            // We can't do anything useful yet here.
        }

        public bool OnWorkerCrashed(Worker worker, string[] crashLogs)
        {
            // If the monitor is present, report the worker crash so it will
            // get attached to the test results (albeit without being associated
            // to a particular test).
            if (_monitor != null)
            {
                _monitor.ReportWorkerCrash(worker, crashLogs);
            }

            if (worker == _controllerWorker)
            {
                // When the controller crashes, we lose track of what tests should running on additional
                // workers, so force the additional workers to restart as well.
                foreach (var additionalWorker in _additionalWorkers)
                {
                    additionalWorker.KillWithRestart("due to the controller crashing");
                }

                // Since this is the controller, we'll automatically be restarted by the main
                // control loop. The Worker class does not need to restart the process.
                return false;
            }
            else
            {
                // It seems like when additional workers crash, they can leave the controller
                // worker in a state where it just stops reporting tests (including it's own). This makes
                // the AutomationRunner think that the test is just taking a really long time.
                //
                // To prevent this, kill the controller worker when an additional worker crashes.
                if (_controllerWorker != null)
                {
                    if (_controllerWorker.Kill())
                    {
                        _testLogger.LogInformation(null, "The controller worker was killed to prevent it stalling after an additional worker crashed.");
                    }
                }

                // Tell the worker it should restart.
                _testLogger.LogInformation(worker, $"Restarting worker #{worker.WorkerNum} because it crashed while performing tests for the controller worker.");
                return true;
            }
        }

        public async Task<TestResults> RunAsync(CancellationToken cancellationToken)
        {
            // Start the additional workers. These will be re-used, even if
            // we need to re-run the controller with a new test prefix.
            if (_additionalWorkers.Count == 0)
            {
                _testLogger.LogTrace(null, $"No additional workers will be started.");
            }
            else
            {
                _testLogger.LogTrace(null, $"Starting {_additionalWorkers.Count} additional workers.");
            }
            var additionalCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var additionalWorkerTasks = _additionalWorkers.Select(x => Task.Run(async () => await x.RunAsync(additionalCancellationTokenSource.Token))).ToList();

            // Iteratively run the controller to get through all of the tests.
            TestResults results = new TestResults();
            for (var attempt = 1; attempt <= Math.Max(_totalAttemptCount, 1); attempt++)
            {
                _testLogger.LogTrace(null, $"Starting controller worker for attempt #{attempt}.");

                // Run the controller worker, and use a monitor to get
                // a live stream of test results.
                _monitor = new JsonResultMonitor<int>(
                    _testLogger,
                    _controllerWorker.RunAsync,
                    _controllerWorker.ReceivedFirstTestInfo,
                    _controllerWorker.TestReportJsonPath,
                    null,
                    _testNotification);
                var exitCode = await _monitor.RunAsync(cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                {
                    throw new TaskCanceledException($"Execution of tests was cancelled", null, cancellationToken);
                }

                _testLogger.LogTrace(null, $"Controller worker has exited with exit code {exitCode}.");

                // Set global information if not already set.
                if (string.IsNullOrWhiteSpace(results.ClientDescriptor))
                {
                    results.ClientDescriptor = _monitor.Results.ClientDescriptor;
                }
                results.TotalDuration += _monitor.Results.TotalDuration;

                // Add any additional worker crashes to the global version.
                results.WorkerCrashes.AddRange(_monitor.Results.WorkerCrashes);

                // If there are any test failures, re-run with the
                // list of failed tests.
                if (attempt < _totalAttemptCount && _monitor.AnyUnsuccessfulTests)
                {
                    // If we got any results at all, adjust the test prefix (otherwise leave it
                    // alone because we didn't get to the point of discovering things).
                    if (_monitor.Results.Results.Count > 0)
                    {
                        // Adjust the test prefix for the next run.
                        _controllerWorker.TestPrefix = string.Join(
                            "+",
                            _monitor.Results.Results
                                .Where(x => x.State != TestState.Success && x.State != TestState.Crash)
                                .Select(x => x.FullTestPath));

                        // Add successful and crashed results to the list because they won't be
                        // run by the next iteration. We don't run crashed results because they're
                        // likely to just crash the process again.
                        results.Results.AddRange(_monitor.Results.Results
                            .Where(x => x.State == TestState.Success || x.State == TestState.Crash));
                    }

                    // Run the controller again with the new list of tests.
                    _testLogger.LogTrace(null, $"One or more tests failed, restarting the controller worker with the subset of failing tests for the next attempt.");
                    continue;
                }
                else
                {
                    // This was either successful, or we're out of retry attempts.
                    // Add all of the results to the list, and then stop iteration.
                    _testLogger.LogTrace(null, $"Either all tests passed, or there are no more remaining attempts to retry failures. Finishing testing.");
                    results.Results.AddRange(_monitor.Results.Results);
                    break;
                }
            }

            // Clear out the monitor.
            _monitor = null;

            // Shutdown the additional workers.
            if (_additionalWorkers.Count > 0)
            {
                _testLogger.LogTrace(null, $"Waiting for additional workers to exit.");
                additionalCancellationTokenSource.Cancel();
                try
                {
                    await Task.WhenAll(additionalWorkerTasks);
                }
                catch (TaskCanceledException)
                {
                    // Expected.
                }
                _testLogger.LogTrace(null, $"Additional workers have exited.");
            }

            return results;
        }
    }
}
