namespace Redpoint.UET.Automation
{
    using System.Diagnostics;

    public class Worker
    {
        private readonly ITestLogger _testLogger;
        private readonly string _sessionGuid;
        private readonly string _sessionName;
        private readonly int _workerNum;
        private readonly string _enginePath;
        private readonly string _projectRoot;
        private readonly string _projectPath;
        private readonly List<string> _cachedCrashLogs;
        private readonly ICrashHooks _crashHooks;

        private Process? _process;
        private string? _restartReason;
        private bool _finishedStartup;
        private bool _shouldRestart;
        private bool _didCrash;
        private Stopwatch? _startupStopwatch;
        private Stopwatch? _idleStopwatch;
        private bool _notifiedOfTestEmit;

        public Worker(
            ITestLogger testLogger,
            string sessionGuid,
            string sessionName,
            int workerNum,
            string enginePath,
            string projectRoot,
            string projectPath,
            ICrashHooks crashHooks)
        {
            _testLogger = testLogger;
            _sessionGuid = sessionGuid;
            _sessionName = sessionName;
            _workerNum = workerNum;
            _enginePath = enginePath;
            _projectRoot = projectRoot;
            _projectPath = projectPath;
            _cachedCrashLogs = new List<string>();
            _crashHooks = crashHooks;
            _notifiedOfTestEmit = false;
        }

        public int WorkerNum => _workerNum;

        protected string ProjectRoot => _projectRoot;

        public void ReceivedFirstTestInfo()
        {
            if (_idleStopwatch != null && _startupStopwatch != null && !_notifiedOfTestEmit)
            {
                _idleStopwatch.Stop();
                _testLogger.LogInformation(
                    this,
                    $"Controller worker idled for {_idleStopwatch.Elapsed.TotalSeconds} seconds after startup.");
                _notifiedOfTestEmit = true;
            }
        }

        private void OnDataReceived(object sender, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                if (_didCrash)
                {
                    _cachedCrashLogs.Add(args.Data.Trim());
                    _testLogger.LogCallstack(this, args.Data.Trim());
                }
                else
                {
                    _testLogger.LogStdout(this, args.Data.Trim());
                }

                if (args.Data.Contains("LogAutomationWorker: Received FindWorkersMessage"))
                {
                    if (!_finishedStartup)
                    {
                        _startupStopwatch!.Stop();
                        _finishedStartup = true;
                        _idleStopwatch = Stopwatch.StartNew();
                        _testLogger.LogInformation(
                            this,
                            $"Worker started in {_startupStopwatch.Elapsed.TotalSeconds} seconds.");
                    }
                }

                if (args.Data.Contains("FSourceFileDatabase::UpdateIfNeeded()"))
                {
                    _shouldRestart = true;
                    _restartReason = "handle the FSourceFileDatabase crash bug";
                }

                if (args.Data.Contains("(RequestedTestFilter & EAutomationTestFlags::FilterMask) != EAutomationTestFlags::SmokeFilter"))
                {
                    _shouldRestart = true;
                    _restartReason = "handle the smoke test filter crash bug";
                }

                if (args.Data.Contains("UnrealEditor-RenderCore.dll!GenerateReferencedUniformBuffers()"))
                {
                    _shouldRestart = true;
                    _restartReason = "handle the uniform buffers crash bug";
                }

                if (args.Data.Contains("LogThreadingWindows: Error: Runnable thread InterchangeWorkerHandler") &&
                    args.Data.Contains("crashed"))
                {
                    _shouldRestart = true;
                    _restartReason = "interchange worker crash bug";
                }

                if (args.Data.Contains("=== Critical error: ==="))
                {
                    if (!_didCrash)
                    {
                        _crashHooks.OnWorkerCrashing(this);
                    }
                    _didCrash = true;
                    _cachedCrashLogs.Add(args.Data.Trim());
                    _testLogger.LogCallstack(this, args.Data.Trim());
                }
            }
        }

        private void OnErrorReceived(object sender, DataReceivedEventArgs args)
        {
            if (!string.IsNullOrWhiteSpace(args.Data))
            {
                _testLogger.LogStderr(
                    this,
                    args.Data.Trim());
            }
        }

        protected virtual IEnumerable<string> GetAdditionalArguments(bool isUnrealEngine5)
        {
            return new string[0];
        }

        public bool Kill()
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    _process.Kill();
                    return true;
                }
            }
            return false;
        }

        public bool KillWithRestart(string restartReason)
        {
            if (_process != null)
            {
                if (!_process.HasExited)
                {
                    _shouldRestart = true;
                    _restartReason = restartReason;
                    _process.Kill();
                    return true;
                }
            }
            return false;
        }

        public async Task<int> RunAsync(CancellationToken cancellationToken)
        {
            if (_process != null)
            {
                throw new InvalidOperationException("Worker is already running.");
            }

            try
            {
                do
                {
                    _restartReason = null;
                    _finishedStartup = false;
                    _shouldRestart = false;
                    _didCrash = false;
                    _startupStopwatch = Stopwatch.StartNew();
                    _cachedCrashLogs.Clear();

                    var unrealEngine4Path = $"{_enginePath}\\Engine\\Binaries\\Win64\\UE4Editor-Cmd.exe";
                    var unrealEngine5Path = $"{_enginePath}\\Engine\\Binaries\\Win64\\UnrealEditor-Cmd.exe";
                    var isUnrealEngine5 = File.Exists(unrealEngine5Path);

                    var logPath = Path.Combine(_projectRoot, "Saved", "Logs", $"Worker{_workerNum}.log");

                    _testLogger.LogTrace(this, $"Worker logs will be emitted to: {logPath}");

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = isUnrealEngine5 ? unrealEngine5Path : unrealEngine4Path,
                        ArgumentList =
                        {
                            _projectPath,
                            "-skipcompile",
                            "-nosplash",
                            "-Unattended",
                            "-NullRHI",
                            "-NOSOUND",
                            "-stdout",
                            "-FullStdOutLogOutput",
                            "-stompmalloc",
                            "-poisonmallocproxy",
                            "-purgatorymallocproxy",
                            $"-SessionId={_sessionGuid}",
                            $"-SessionName={_sessionName}",
                            $"-AutomationWorkerNum={_workerNum}",
                            $"-abslog={logPath}",
                            // Prevents errors from other workers relayed onto the primary worker from causing tests on the primary worker to unexpectedly fail.
                            "-ini:Engine:[Core.Log]:LogAutomationController=NoLogging",
                            // The engine will check for UObject leaks inside world memory and force a crash if it detects them. However, these are unrelated to
                            // automation tests (which don't operate in the world for the most part), so we want to avoid these crashes.
                            "-ini:Engine:[Core.Log]:LogEditorServer=NoLogging",
                            // We used to set the DeviceTag with $"-DeviceTag=Worker{_workerNum}" here, but this causes
                            // tests to be duplicated across workers, rather than distributed across workers.
                            //
                            // DeviceTag effectively overrides the "hostname" of the device, which changes the way
                            // the "instance" field is calculated in the test report. The intent was this would allow
                            // us to know which worker a particular test was running on.
                            //
                            // The issue is that the Device Groups feature of automation is by default "Machine Name",
                            // and there's no way to change this via the command line. The only way to set it is by
                            // manually changing the selected device groups in the UI or by a C++ call, neither of which
                            // is feasible for AutomationRunner to do.
                            //
                            // We now force the DeviceTag to be the same on all workers, since that is what test distribution
                            // is controlled by.
                            "-DeviceTag=Automation"
                        },
                        UseShellExecute = false,
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        RedirectStandardInput = true,
                    };
                    foreach (var additionalArgument in GetAdditionalArguments(isUnrealEngine5))
                    {
                        startInfo.ArgumentList.Add(additionalArgument);
                    }

                    _process = Process.Start(startInfo);
                    if (_process == null)
                    {
                        throw new InvalidOperationException("Failed to start Unreal Engine process.");
                    }

                    // Ensure that the controller worker starts up as fast as possible.
                    if (Environment.GetEnvironmentVariable("CI") == "true")
                    {
                        try
                        {
                            _process.PriorityClass = WorkerNum == 1 ? ProcessPriorityClass.High : ProcessPriorityClass.AboveNormal;
                        }
                        catch (Exception ex)
                        {
                            _testLogger.LogWarning(this, $"Unable to adjust process priority: {ex.Message}");
                        }
                    }

                    _process.StandardInput.Close();
                    _process.OutputDataReceived += OnDataReceived;
                    _process.ErrorDataReceived += OnErrorReceived;
                    _process.BeginOutputReadLine();
                    _process.BeginErrorReadLine();

                    try
                    {
                        await _process.WaitForExitAsync(cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        if (!_process.HasExited)
                        {
                            // Try to the terminate the process since we're cancelling.
                            try
                            {
                                _process.Kill();
                            }
                            catch { }
                        }

                        throw;
                    }

                    if (!_shouldRestart && _didCrash)
                    {
                        if (_crashHooks.OnWorkerCrashed(this, _cachedCrashLogs.ToArray()))
                        {
                            // We are being told that we should automatically restart by the crash handler.
                            _shouldRestart = true;
                            _restartReason = "due to handled crash";
                        }
                    }

                    if (!_shouldRestart)
                    {
                        _testLogger.LogInformation(this, $"Worker exited with exit code {_process.ExitCode}.");
                    }
                    else
                    {
                        _testLogger.LogWarning(this, $"Worker restarting to {_restartReason}.");
                    }
                } while (_shouldRestart);

                _testLogger.LogTrace(this, $"Returning exit code from worker.");

                return _process.ExitCode;
            }
            finally
            {
                _process = null;
            }
        }
    }
}
