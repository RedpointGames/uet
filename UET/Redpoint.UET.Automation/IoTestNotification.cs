namespace Redpoint.UET.Automation
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;

    public class IoTestNotification : ITestNotification
    {
        private readonly Task _submitTask;
        private readonly ConcurrentQueue<IoChange> _changeQueue;
        private readonly ITestLogger _testLogger;
        private readonly bool _simulateIo;

        public IoTestNotification(ITestLogger testLogger, bool simulateIo)
        {
            CancellationTokenSource = new CancellationTokenSource();
            _submitTask = Task.Run(RunAsync);
            _changeQueue = new ConcurrentQueue<IoChange>();
            _testLogger = testLogger;
            _simulateIo = simulateIo;

            if (_simulateIo)
            {
                _testLogger.LogInformation(null, $"Results will be simulated to an Io build monitor");
            }
            else
            {
                _testLogger.LogInformation(null, $"Results will be submitted live to the Io build monitor at: {Environment.GetEnvironmentVariable("IO_URL")}");
            }
        }

        public CancellationTokenSource CancellationTokenSource { get; set; }

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
            var dateStarted = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(testResult.DateTime))
            {
                DateTimeOffset.TryParseExact(testResult.DateTime, "yyyy.MM.dd-HH.mm.ss", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateStarted);
            }

            _changeQueue.Enqueue(new IoChange
            {
                FullName = testResult.FullTestPath,
                Platform = "Win64",
                GauntletInstance = null,
                AutomationInstance = testResult.DeviceInstance.FirstOrDefault() ?? "Unknown",
                Status = "running",
                IsGauntlet = false,
                DateStartedUtc = dateStarted.ToUnixTimeMilliseconds(),
                DateFinishedUtc = null,
                DurationSeconds = null,
            });
        }

        public void TestFinished(TestResult testResult)
        {
            if (testResult.State == TestState.Success)
            {
                _changeQueue.Enqueue(new IoChange
                {
                    FullName = testResult.FullTestPath,
                    Platform = "Win64",
                    GauntletInstance = null,
                    AutomationInstance = testResult.DeviceInstance.FirstOrDefault() ?? "Unknown",
                    Status = "passed",
                    IsGauntlet = false,
                    DateStartedUtc = null,
                    DateFinishedUtc = null,
                    DurationSeconds = testResult.Duration,
                    // @todo: Append primary log lines? They aren't surfaced anywhere in Io at the moment though.
                });
            }
            else
            {
                _changeQueue.Enqueue(new IoChange
                {
                    FullName = testResult.FullTestPath,
                    Platform = "Win64",
                    GauntletInstance = null,
                    AutomationInstance = testResult.DeviceInstance.FirstOrDefault() ?? "Unknown",
                    Status = "failed",
                    IsGauntlet = false,
                    DateStartedUtc = null,
                    DateFinishedUtc = null,
                    DurationSeconds = testResult.Duration,
                    // @todo: Append primary log lines? They aren't surfaced anywhere in Io at the moment though.
                });
            }
        }

        public async Task WaitAsync()
        {
            await _submitTask;
        }

        private async Task SubmitResultsAsync(List<IoChange> changes)
        {
            if (_simulateIo)
            {
                _testLogger.LogTrace(null, $"This JSON would be submitted to Io: {JsonSerializer.Serialize(changes)}");
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
                        _testLogger.LogWarning(null, $"Failed to send test results to Io build monitor.\nStatus Code: {response.StatusCode}\nBody: {response.Content.ReadAsStringAsync()}");
                    }
                }
            }
        }

        private async Task RunAsync()
        {
            try
            {
                while (!CancellationTokenSource.IsCancellationRequested)
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
                            await Task.Delay(1000, CancellationTokenSource.Token);
                            continue;
                        }

                        await SubmitResultsAsync(results);
                    }
                    catch (TaskCanceledException)
                    {
                    }
                    catch (Exception ex)
                    {
                        _testLogger.LogException(null, ex, "Exception while reporting data to Io");
                        await Task.Delay(1000, CancellationTokenSource.Token);
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _testLogger.LogTrace(null, "Performing final submission to Io");

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
                    _testLogger.LogTrace(null, $"There are {results.Count} changes to include in the final submission to Io");
                    await SubmitResultsAsync(results);
                }
                else
                {
                    _testLogger.LogTrace(null, "There was nothing to include in the final submission to Io");
                }
            }
        }
    }
}
