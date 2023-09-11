namespace Redpoint.Uet.Automation.Worker.Local
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading.Tasks;

    internal sealed class LocalEditorWorker : LocalWorker, ICaptureSpecification
    {
        private readonly ILogger<LocalEditorWorker> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly ILoopbackPortReservation _portReservation;
        private readonly OnWorkerStarted _onWorkerStarted;
        private readonly OnWorkerExited _onWorkerExited;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _backgroundTask;
        private TimeSpan _startupDuration;

        public LocalEditorWorker(
            ILogger<LocalEditorWorker> logger,
            IProcessExecutor processExecutor,
            string id,
            string displayName,
            ILoopbackPortReservation portReservation,
            DesiredWorkerDescriptor descriptor,
            OnWorkerStarted onWorkerStarted,
            OnWorkerExited onWorkerExited)
        {
            _logger = logger;
            _processExecutor = processExecutor;

            Id = id;
            DisplayName = displayName;
            _portReservation = portReservation;
            Descriptor = descriptor;
            _startupDuration = TimeSpan.Zero;
            _onWorkerStarted = onWorkerStarted;
            _onWorkerExited = onWorkerExited;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override string Id { get; }

        public override string DisplayName { get; }

        public override DesiredWorkerDescriptor Descriptor { get; }

        public override IPEndPoint EndPoint => _portReservation.EndPoint;

        public override TimeSpan StartupDuration => _startupDuration;

        private async Task BackgroundLoopAsync()
        {
            var configurationInfo = Descriptor.Configuration == "DebugGame" ? $"-{Descriptor.Platform}-DebugGame" : string.Empty;

            var editorPath = Path.Combine(Descriptor.EnginePath!, "Engine", "Binaries", Descriptor.Platform, $"UnrealEditor{configurationInfo}-Cmd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

            var projectRoot = Path.GetDirectoryName(Descriptor.UProjectPath)!;

            var logPath = Descriptor.WorkerLogsPath != null
                ? Path.Combine(Descriptor.WorkerLogsPath, $"Worker_{Descriptor.Platform}_{Id}.log")
                : Path.Combine(projectRoot, "Saved", "Logs", $"Worker_{Descriptor.Platform}_{Id}.log");

            // Before we run Unreal, we must add an exception for the port if it does not already exist.
            if (OperatingSystem.IsWindows())
            {
                var sb = new StringBuilder();
                await _processExecutor.ExecuteAsync(
                    new ProcessSpecification
                    {
                        FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe"),
                        Arguments = new[]
                        {
                            "advfirewall",
                            "firewall",
                            "show",
                            "rule",
                            @$"name=""UET_{EndPoint.Port}"""
                        }
                    },
                    CaptureSpecification.CreateFromStdoutStringBuilder(sb),
                    _cancellationTokenSource.Token).ConfigureAwait(false);
                if (sb.ToString().Contains("no rules", StringComparison.Ordinal))
                {
                    _logger.LogInformation($"Adding firewall rule to permit port {EndPoint.Port}...");
                    await _processExecutor.ExecuteAsync(
                        new ProcessSpecification
                        {
                            FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "netsh.exe"),
                            Arguments = new[]
                            {
                                "advfirewall",
                                "firewall",
                                "add",
                                "rule",
                                @$"name=""UET_{EndPoint.Port}""",
                                "dir=in",
                                "action=allow",
                                "protocol=TCP",
                                $"localport={EndPoint.Port}"
                            }
                        },
                        CaptureSpecification.Passthrough,
                        _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }

            // Compute the arguments for Unreal Engine.
            var arguments = new List<string>
            {
                Descriptor.UProjectPath,
                "-skipcompile",
                "-Unattended",
                "-stdout",
                "-FullStdOutLogOutput",
                "-stompmalloc",
                "-poisonmallocproxy",
                "-purgatorymallocproxy",
                "-Messaging",
                "-CrashForUAT",
                $"-SessionId={Id.ToString().Replace("-", "", StringComparison.Ordinal).Replace("{", "", StringComparison.Ordinal).Replace("}", "", StringComparison.Ordinal).ToUpperInvariant()}",
                $"-SessionName={DisplayName}",
                $"-SessionOwner={(Environment.UserName.Contains('$', StringComparison.Ordinal) ? "SYSTEM" : Environment.UserName)}",
                $"-abslog={logPath}",
                // The engine will check for UObject leaks inside world memory and force a crash if it detects them. However, these are unrelated to
                // automation tests (which don't operate in the world for the most part), so we want to avoid these crashes.
                "-ini:Engine:[Core.Log]:LogEditorServer=NoLogging",
                // Configure messaging transports so that our C# libraries can talk
                // to this instance.
                "-EnablePlugins=TcpMessaging",
                "-DisablePlugins=UdpMessaging",
                "-ini:Engine:[/Script/TcpMessaging.TcpMessagingSettings]:EnableTransport=True",
                $"-ini:Engine:[/Script/TcpMessaging.TcpMessagingSettings]:ListenEndpoint={EndPoint}",
                "-ini:Engine:[/Script/UdpMessaging.UdpMessagingSettings]:EnabledByDefault=False",
                "-ini:Engine:[/Script/UdpMessaging.UdpMessagingSettings]:EnableTransport=False",
                // Increase logging for TCP messaging.
                "-ini:Engine:[Core.Log]:LogTcpMessaging=VeryVerbose",
            };
            if (!Descriptor.EnableRendering)
            {
                arguments.AddRange(new[]
                {
                    "-nosplash",
                    "-NullRHI",
                    "-NOSOUND",
                });
            }

            // Start the process in the background.
            var st = Stopwatch.StartNew();
            var automationTask = Task.Run(async () => await _processExecutor.ExecuteAsync(
                new ProcessSpecification
                {
                    FilePath = editorPath,
                    Arguments = arguments,
                    WorkingDirectory = Descriptor.EnginePath,
                },
                this,
                _cancellationTokenSource.Token).ConfigureAwait(false));

            var didFireExit = false;
            try
            {
                // Wait for startup.
                do
                {
                    if (automationTask.IsCanceled)
                    {
                        _logger.LogWarning($"[{Id}] Executable exited with no exit code information, because the task was cancelled");
                        didFireExit = true;
                        await _onWorkerExited(this, int.MinValue, null).ConfigureAwait(false);
                        return;
                    }
                    if (automationTask.IsFaulted)
                    {
                        _logger.LogError($"[{Id}] Executable exited with no exit code information, because the task fired an exception: {automationTask.Exception}");
                        didFireExit = true;
                        await _onWorkerExited(this, int.MinValue, null).ConfigureAwait(false);
                        return;
                    }
                    if (automationTask.IsCompletedSuccessfully)
                    {
                        _logger.LogWarning($"[{Id}] Executable exited unexpectedly with exit code {automationTask.Result}");
                        didFireExit = true;
                        await _onWorkerExited(this, automationTask.Result, await GrabCrashDataFromLogs(logPath).ConfigureAwait(false)).ConfigureAwait(false);
                        return;
                    }

                    try
                    {
                        var client = new TcpClient();
                        client.Connect(EndPoint);
                        break;
                    }
                    catch (SocketException ex) when (ex.ErrorCode == 10061)
                    {
                        _logger.LogTrace($"[{Id}] Still waiting to be able to connect...");
                        continue;
                    }
                    catch (IOException ex) when (ex.Message.Contains("An existing connection was forcibly closed by the remote host.", StringComparison.Ordinal))
                    {
                        _logger.LogTrace($"[{Id}] Connection was unexpectedly disconnected.");
                        continue;
                    }
                }
                while (!_cancellationTokenSource.IsCancellationRequested);

                // We have now started.
                st.Stop();
                _startupDuration = st.Elapsed;
                _logger.LogTrace($"[{Id}] Worker ready in {_startupDuration.TotalSeconds} secs.");
                await _onWorkerStarted(this).ConfigureAwait(false);

                // Wait until we are cancelled or the process exits.
                while (!_cancellationTokenSource.IsCancellationRequested &&
                       !automationTask.IsCompleted)
                {
                    // @note: This will throw, but DisposeAsync will eat the exception.
                    await Task.Delay(500, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            finally
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    _cancellationTokenSource.Cancel();
                }
                try
                {
                    await automationTask.ConfigureAwait(false);
                }
                catch
                {
                }
                if (!didFireExit)
                {
                    if (automationTask.IsCanceled)
                    {
                        _logger.LogTrace($"[{Id}] Executable exited with no exit code information, because the task was cancelled");
                        await _onWorkerExited(this, int.MinValue, null).ConfigureAwait(false);
                    }
                    if (automationTask.IsFaulted)
                    {
                        _logger.LogError($"[{Id}] Executable exited with no exit code information, because the task fired an exception: {automationTask.Exception}");
                        await _onWorkerExited(this, int.MinValue, null).ConfigureAwait(false);
                    }
                    if (automationTask.IsCompletedSuccessfully)
                    {
                        _logger.LogTrace($"[{Id}] Executable exited with exit code {automationTask.Result}");
                        await _onWorkerExited(this, automationTask.Result, await GrabCrashDataFromLogs(logPath).ConfigureAwait(false)).ConfigureAwait(false);
                    }
                }
            }
        }

        private static async Task<IWorkerCrashData?> GrabCrashDataFromLogs(string logPath)
        {
            do
            {
                try
                {
                    var foundCrash = false;
                    var crashLines = new List<string>();
                    using (var reader = new StreamReader(new FileStream(logPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)))
                    {
                        while (!reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync().ConfigureAwait(false);
                            if (line!.Contains("=== Critical error: ===", StringComparison.Ordinal))
                            {
                                foundCrash = true;
                            }
                            if (foundCrash)
                            {
                                crashLines.Add(line[(line.IndexOf("Error:", StringComparison.Ordinal) + "Error:".Length)..].Trim());
                            }
                            if (line.Contains("end: stack for UAT", StringComparison.Ordinal))
                            {
                                break;
                            }
                        }
                    }
                    if (foundCrash)
                    {
                        return new LocalWorkerCrashData(string.Join("\n", crashLines));
                    }
                    return null;
                }
                catch (IOException ex) when (ex.Message.Contains("used by another process", StringComparison.Ordinal))
                {
                    // Still waiting on Unreal to finish with the log file.
                    await Task.Delay(500).ConfigureAwait(false);
                    continue;
                }
            } while (true);
        }

        public override void StartInBackground()
        {
            if (_backgroundTask == null)
            {
                _backgroundTask = Task.Run(BackgroundLoopAsync);
            }
        }

        public override async ValueTask DisposeAsync()
        {
            _cancellationTokenSource.Cancel();
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }
            await _portReservation.DisposeAsync().ConfigureAwait(false);
            _cancellationTokenSource.Dispose();
        }

        bool ICaptureSpecification.InterceptStandardInput => true;

        bool ICaptureSpecification.InterceptStandardOutput => true;

        bool ICaptureSpecification.InterceptStandardError => true;

        string? ICaptureSpecification.OnRequestStandardInputAtStartup()
        {
            return null;
        }

        void ICaptureSpecification.OnReceiveStandardOutput(string data)
        {
            _logger.LogTrace($"[{Id}] [stdout] {data.Trim()}");
        }

        void ICaptureSpecification.OnReceiveStandardError(string data)
        {
            _logger.LogTrace($"[{Id}] [stderr] {data.Trim()}");
        }
    }
}
