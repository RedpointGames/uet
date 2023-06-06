namespace Redpoint.UET.Automation.TestNotification.Io
{
    using Microsoft.Extensions.Logging;
    using Redpoint.UET.Automation.Model;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;

    internal class IoTestNotification : ITestNotification
    {
        private readonly Task _submitTask;
        private readonly ConcurrentQueue<IoChange> _changeQueue;
        private readonly ILogger<IoTestNotification> _logger;
        private readonly CancellationToken _cancellationToken;
        private readonly bool _simulateIo;

        internal static bool IsIoAvailable()
        {
            return !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("IO_URL")) &&
                !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CI_JOB_JWT_V1"));
        }

        public IoTestNotification(
            ILogger<IoTestNotification> logger,
            CancellationToken cancellationToken,
            bool simulateIo)
        {
            _submitTask = Task.Run(RunAsync);
            _changeQueue = new ConcurrentQueue<IoChange>();
            _logger = logger;
            _cancellationToken = cancellationToken;
            _simulateIo = simulateIo;

            if (_simulateIo)
            {
                _logger.LogInformation($"Results will be simulated to an Io build monitor");
            }
            else
            {
                _logger.LogInformation($"Results will be submitted live to the Io build monitor at: {Environment.GetEnvironmentVariable("IO_URL")}");
            }
        }

        public void TestDiscovered(TestResult testResult)
        {
            _changeQueue.Enqueue(new IoChange
            {
                FullName = testResult.FullTestPath,
                Platform = "Win64",
                GauntletInstance = null,
                AutomationInstance = null,
                Status = "listed",
                IsGauntlet = false,
                DateStartedUtc = null,
                DateFinishedUtc = null,
                DurationSeconds = null,
            });
        }

        public void TestStarted(TestResult testResult)
        {
            _changeQueue.Enqueue(new IoChange
            {
                FullName = testResult.FullTestPath,
                Platform = "Win64",
                GauntletInstance = null,
                AutomationInstance = testResult.WorkerDisplayName ?? "Unknown",
                Status = "running",
                IsGauntlet = false,
                DateStartedUtc = testResult.DateStarted.ToUnixTimeMilliseconds(),
                DateFinishedUtc = null,
                DurationSeconds = null,
            });
        }

        public void TestFinished(TestResult testResult)
        {
            if (testResult.TestStatus == TestResultStatus.Passed)
            {
                _changeQueue.Enqueue(new IoChange
                {
                    FullName = testResult.FullTestPath,
                    Platform = testResult.Platform,
                    GauntletInstance = null,
                    AutomationInstance = testResult.WorkerDisplayName ?? "Unknown",
                    Status = "passed",
                    IsGauntlet = false,
                    DateStartedUtc = null,
                    DateFinishedUtc = null,
                    DurationSeconds = testResult.Duration.TotalSeconds,
                    // @todo: Append primary log lines? They aren't surfaced anywhere in Io at the moment though.
                });
            }
            else
            {
                _changeQueue.Enqueue(new IoChange
                {
                    FullName = testResult.FullTestPath,
                    Platform = testResult.Platform,
                    GauntletInstance = null,
                    AutomationInstance = testResult.WorkerDisplayName ?? "Unknown",
                    Status = "failed",
                    IsGauntlet = false,
                    DateStartedUtc = null,
                    DateFinishedUtc = null,
                    DurationSeconds = testResult.Duration.TotalSeconds,
                    // @todo: Append primary log lines? They aren't surfaced anywhere in Io at the moment though.
                });
            }
        }

        public async Task FlushAsync()
        {
            await _submitTask;
        }

        private async Task SubmitResultsAsync(List<IoChange> changes)
        {
            if (_simulateIo)
            {
                _logger.LogTrace($"This JSON would be submitted to Io: {JsonSerializer.Serialize(changes)}");
            }
            else
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {Environment.GetEnvironmentVariable("CI_JOB_JWT_V1")}");

                    var response = await client.PutAsJsonAsync(
                        $"{(Environment.GetEnvironmentVariable("IO_URL") ?? string.Empty).TrimEnd('/')}/api/submit/tests",
                        changes);
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning($"Failed to send test results to Io build monitor.\nStatus Code: {response.StatusCode}\nBody: {response.Content.ReadAsStringAsync()}");
                    }
                }
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (!_cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var results = new List<IoChange>();
                        while (_changeQueue.TryDequeue(out IoChange? result))
                        {
                            if (result != null)
                            {
                                results.Add(result);
                            }
                        }
                        if (results.Count == 0)
                        {
                            await Task.Delay(1000, _cancellationToken);
                            continue;
                        }

                        await SubmitResultsAsync(results);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Exception while reporting data to Io: {ex.Message}");
                        await Task.Delay(1000, _cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogTrace("Performing final submission to Io");

                var results = new List<IoChange>();
                while (_changeQueue.TryDequeue(out IoChange? result))
                {
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }

                if (results.Count > 0)
                {
                    _logger.LogTrace($"There are {results.Count} changes to include in the final submission to Io");
                    await SubmitResultsAsync(results);
                }
                else
                {
                    _logger.LogTrace("There was nothing to include in the final submission to Io");
                }
            }
        }
    }
}
