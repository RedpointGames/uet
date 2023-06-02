namespace Redpoint.UET.Automation
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Text.Json;
    using System.Threading.Tasks;

    public static class JsonElementExtensions
    {
        public static JsonElement? GetOptionalProperty(this JsonElement self, string propertyName)
        {
            if (self.TryGetProperty(propertyName, out var value))
            {
                return value;
            }
            return null;
        }
    }

    public class JsonResultMonitor<T>
    {
        private readonly ITestLogger _testLogger;
        private readonly Func<CancellationToken, Task<T>> _targetTask;
        private readonly Action _onFirstTestDataReceived;
        private readonly string _jsonFilePath;
        private readonly Func<TestResult, Worker?>? _resolveWorker;
        private readonly ITestNotification? _testNotification;
        private readonly HashSet<string> _testsDiscovered;
        private readonly HashSet<string> _testsStarted;
        private readonly HashSet<string> _testsFinished;
        private readonly Dictionary<string, string[]?> _testsCrashed;
        private readonly List<(Worker worker, string[] crashLogs)> _workerCrashes;
        private readonly Stopwatch _runStopwatch;
        private Stopwatch? _idlingStopwatch;

        public JsonResultMonitor(
            ITestLogger testLogger,
            Func<CancellationToken, Task<T>> targetTask,
            Action onFirstTestDataReceived,
            string jsonFilePath,
            Func<TestResult, Worker?>? resolveWorker,
            ITestNotification? testNotification)
        {
            _testLogger = testLogger;
            _targetTask = targetTask;
            _onFirstTestDataReceived = onFirstTestDataReceived;
            _jsonFilePath = jsonFilePath;
            _resolveWorker = resolveWorker;
            _testNotification = testNotification;
            _testsDiscovered = new HashSet<string>();
            _testsStarted = new HashSet<string>();
            _testsFinished = new HashSet<string>();
            _testsCrashed = new Dictionary<string, string[]?>();
            _workerCrashes = new List<(Worker worker, string[] crashLogs)>();
            _runStopwatch = Stopwatch.StartNew();
        }

        public TestResults Results { get; private set; } = new TestResults();

        public bool AnyUnsuccessfulTests => Results.AnyRetryableFailures;

        public void ReportWorkerCrash(Worker worker, string[] crashLogs)
        {
            _workerCrashes.Add((worker, crashLogs));
        }

        public async Task<T> RunAsync(CancellationToken cancellationToken)
        {
            if (File.Exists(_jsonFilePath))
            {
                // Ensure we can't accidentally use data from previous runs.
                File.Delete(_jsonFilePath);
            }

            var backgroundCompleteCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var backgroundTask = Task.Run(async () =>
            {
                try
                {
                    _testLogger.LogTrace(null, $"JSON monitor is starting background task.");
                    return await _targetTask(cancellationToken);
                }
                catch (Exception ex)
                {
                    _testLogger.LogException(null, ex, "Exception while running monitored task");
                    throw;
                }
                finally
                {
                    _testLogger.LogTrace(null, $"JSON monitor is now cancelling pending operations.");
                    backgroundCompleteCts.Cancel();
                }
            });

            // FileSystemWatcher does not work for the types of writes
            // that Unreal Engine does for index.json. I have no idea
            // why, given that the attributes do update on disk, but
            // FSM just never fires the events. So instead, we poll
            // every 2 milliseconds to try and get accurate 
            // timing information.

            try
            {
                while (!backgroundCompleteCts.IsCancellationRequested)
                {
                    if (File.Exists(_jsonFilePath))
                    {
                        try
                        {
                            Results = ProcessJsonFile(_jsonFilePath);
                            ProcessEvents();
                        }
                        catch (IOException ex) when (ex.Message.Contains("it is being used by another process"))
                        {
                            // Can't process right now because Unreal is writing the file out. Fallthrough
                            // to the delay.
                        }
                    }

                    await Task.Delay(2, backgroundCompleteCts.Token);
                }

                _testLogger.LogTrace(null, $"JSON monitor has exited monitoring loop normally.");
            }
            catch (OperationCanceledException)
            {
                _testLogger.LogTrace(null, $"JSON monitor is finishing monitoring work due to OperationCanceledException.");
            }
            finally
            {
                _testLogger.LogTrace(null, $"JSON monitor is reading the results file before returning.");

                if (File.Exists(_jsonFilePath))
                {
                    // Expected. Do one final processing of the JSON results before returning.
                    for (int i = 0; i < 30; i++)
                    {
                        try
                        {
                            Results = ProcessJsonFile(_jsonFilePath);
                            ProcessEvents();
                            break;
                        }
                        catch (IOException ex) when (ex.Message.Contains("it is being used by another process"))
                        {
                            // Wait a bit to see if we can get the final results.
                            await Task.Delay(2);
                        }
                    }
                }
            }

            if (backgroundTask.IsCompletedSuccessfully)
            {
                _testLogger.LogTrace(null, $"JSON monitor is returning results (completed).");
                return backgroundTask.Result;
            }
            else if (backgroundTask.IsFaulted)
            {
                _testLogger.LogTrace(null, $"JSON monitor is returning results (faulted).");
                throw backgroundTask.Exception!;
            }
            else if (backgroundTask.IsCanceled)
            {
                _testLogger.LogTrace(null, $"JSON monitor is returning results (cancelled).");
                return default!;
            }
            else
            {
                _testLogger.LogTrace(null, $"JSON monitor is returning results (incomplete).");
                return default!;
            }
        }

        private void ProcessEvents()
        {
            foreach (var result in Results.Results)
            {
                if (!_testsDiscovered.Contains(result.FullTestPath))
                {
                    _idlingStopwatch?.Restart();
                    _testLogger.LogDiscovered(null, result);
                    _testNotification?.TestDiscovered(result);
                    if (_testsDiscovered.Count == 0)
                    {
                        _onFirstTestDataReceived();
                    }
                    _testsDiscovered.Add(result.FullTestPath);
                }

                if (_testsDiscovered.Count > 0 && _idlingStopwatch == null)
                {
                    _idlingStopwatch = Stopwatch.StartNew();
                }

                if (!_testsStarted.Contains(result.FullTestPath) &&
                    result.State != TestState.NotRun)
                {
                    _idlingStopwatch?.Restart();
                    _testLogger.LogStarted(_resolveWorker?.Invoke(result), result);
                    _testNotification?.TestStarted(result);
                    _testsStarted.Add(result.FullTestPath);
                }

                if (!_testsFinished.Contains(result.FullTestPath) &&
                    result.State != TestState.NotRun &&
                    result.State != TestState.InProcess)
                {
                    _idlingStopwatch?.Restart();

                    foreach (var entry in result.Entries)
                    {
                        if (entry.Event.Type == "Error")
                        {
                            _testLogger.LogError(null, $"{result.FullTestPath}: {entry.Event.Message}");
                        }
                        else if (entry.Event.Type == "Warning")
                        {
                            _testLogger.LogWarning(null, $"{result.FullTestPath}: {entry.Event.Message}");
                        }
                        else if (entry.Event.Type == "Crash")
                        {
                            // This will have already been logged when the crash originally happened, so we don't need to log it again.
                        }
                    }

                    _testLogger.LogFinished(_resolveWorker?.Invoke(result), result);
                    _testNotification?.TestFinished(result);
                    _testsFinished.Add(result.FullTestPath);
                }
            }

            if (_idlingStopwatch != null && _idlingStopwatch.Elapsed.TotalSeconds > 10)
            {
                var waitingOn = Results.Results.Where(x => x.State == TestState.InProcess).ToList();

                _testLogger.LogWaiting(null, $"[{_runStopwatch.Elapsed}] We're still waiting on {waitingOn.Count} {(waitingOn.Count != 1 ? "tests" : "test")}:");
                foreach (var testResult in waitingOn)
                {
                    _testLogger.LogWaiting(_resolveWorker?.Invoke(testResult), testResult.FullTestPath);
                }

                _idlingStopwatch = null;
            }
        }

        private TestResults ProcessJsonFile(string jsonFilePath)
        {
            var results = new TestResults();

            // Associate additional worker crashes.
            results.WorkerCrashes = _workerCrashes;

            JsonDocument document;
            using (var stream = new FileStream(jsonFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using (var reader = new StreamReader(stream))
                {
                    document = JsonDocument.Parse(reader.ReadToEnd());
                }
            }

            if (document.RootElement.TryGetProperty("clientDescriptor", out var prop))
            {
                results.ClientDescriptor = prop.GetString() ?? string.Empty;
            }
            if (string.IsNullOrWhiteSpace(results.ClientDescriptor))
            {
                if (document.RootElement.TryGetProperty("devices", out var devices))
                {
                    for (var d = 0; d < devices.GetArrayLength(); d++)
                    {
                        var device = devices[d];

                        if (document.RootElement.TryGetProperty("instance", out var instance))
                        {
                            results.ClientDescriptor = instance.GetString() ?? string.Empty;
                            if (!string.IsNullOrWhiteSpace(results.ClientDescriptor))
                            {
                                break;
                            }
                        }
                    }
                }
            }
            if (document.RootElement.TryGetProperty("totalDuration", out var totalDuration))
            {
                results.TotalDuration = totalDuration.GetDouble();
            }

            if (document.RootElement.TryGetProperty("tests", out var tests))
            {
                for (int i = 0; i < tests.GetArrayLength(); i++)
                {
                    try
                    {
                        var test = tests[i];

                        var testResult = new TestResult
                        {
                            TestDisplayName = test.GetOptionalProperty("testDisplayName")?.GetString() ?? string.Empty,
                            FullTestPath = test.GetOptionalProperty("fullTestPath")?.GetString() ?? string.Empty,
                            DateTime = test.GetOptionalProperty("dateTime")?.GetString() ?? string.Empty,
                            Duration = test.GetOptionalProperty("duration")?.GetDouble() ?? 0.0,
                            Warnings = test.GetOptionalProperty("warnings")?.GetInt32() ?? 0,
                            Errors = test.GetOptionalProperty("errors")?.GetInt32() ?? 0,
                        };

                        if (_testsCrashed.ContainsKey(testResult.FullTestPath))
                        {
                            testResult.State = TestState.Crash;
                        }
                        else
                        {
                            switch (test.GetOptionalProperty("state")?.GetString())
                            {
                                case "NotRun":
                                    testResult.State = TestState.NotRun;
                                    break;
                                case "InProcess":
                                    testResult.State = TestState.InProcess;
                                    break;
                                case "Success":
                                    testResult.State = TestState.Success;
                                    break;
                                case "Fail":
                                    testResult.State = TestState.Fail;
                                    break;
                                default:
                                    // Default to failure, since we're not handling this state.
                                    testResult.State = TestState.Fail;
                                    break;
                            }
                        }

                        if (test.TryGetProperty("deviceInstance", out var deviceInstance))
                        {
                            if (deviceInstance.ValueKind == JsonValueKind.Array)
                            {
                                testResult.DeviceInstance = Enumerable.Range(0, deviceInstance.GetArrayLength()).Select(d => deviceInstance[d].GetString() ?? string.Empty).Where(x => !string.IsNullOrEmpty(x)).ToArray() ?? new string[0];
                            }
                            else if (deviceInstance.ValueKind == JsonValueKind.String)
                            {
                                testResult.DeviceInstance = new[] { deviceInstance.GetString() ?? string.Empty };
                            }
                        }

                        if (test.TryGetProperty("entries", out var entries))
                        {
                            var resultEntries = new List<TestResultEntry>();
                            for (var e = 0; e < entries.GetArrayLength(); e++)
                            {
                                var entry = entries[e];
                                resultEntries.Add(new TestResultEntry
                                {
                                    Filename = entry.GetOptionalProperty("filename")?.GetString() ?? string.Empty,
                                    LineNumber = entry.GetOptionalProperty("lineNumber")?.GetInt32() ?? 0,
                                    Timestamp = entry.GetOptionalProperty("timestamp")?.GetString() ?? string.Empty,
                                    Event =
                                    {
                                        Type = entry.GetOptionalProperty("event")?.GetOptionalProperty("type")?.GetString() ?? string.Empty,
                                        Message = entry.GetOptionalProperty("event")?.GetOptionalProperty("message")?.GetString() ?? string.Empty,
                                        Context = entry.GetOptionalProperty("event")?.GetOptionalProperty("context")?.GetString() ?? string.Empty,
                                        Artifact = entry.GetOptionalProperty("event")?.GetOptionalProperty("artifact")?.GetString() ?? string.Empty,
                                    }
                                });
                            }
                            if (_testsCrashed.ContainsKey(testResult.FullTestPath) &&
                                _testsCrashed[testResult.FullTestPath] != null)
                            {
                                resultEntries.Add(new TestResultEntry
                                {
                                    Filename = string.Empty,
                                    LineNumber = 0,
                                    Timestamp = string.Empty,
                                    Event =
                                    {
                                        Type = "Crash",
                                        Message = string.Join("\n", _testsCrashed[testResult.FullTestPath] ?? new string[0]),
                                        Context = string.Empty,
                                        Artifact = string.Empty,
                                    }
                                });
                            }
                            testResult.Entries = resultEntries.ToArray();
                        }

                        results.Results.Add(testResult);
                    }
                    catch (KeyNotFoundException ex)
                    {
                        // This test result doesn't have the required properties for processing.
                        _testLogger.LogWarning(null, $"Missing required property on test, it will be ignored: {ex.Message}");
                    }
                }
            }

            return results;
        }
    }
}
