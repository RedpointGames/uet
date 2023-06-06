namespace Redpoint.UET.Automation.Worker.Local
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading.Tasks;

    internal class LocalEditorWorker : LocalWorker, ICaptureSpecification
    {
        private readonly ILogger<LocalEditorWorker> _logger;
        private readonly IProcessExecutor _processExecutor;

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
            int reservedPort,
            DesiredWorkerDescriptor descriptor,
            OnWorkerStarted onWorkerStarted,
            OnWorkerExited onWorkerExited)
        {
            _logger = logger;
            _processExecutor = processExecutor;

            Id = id;
            DisplayName = displayName;
            Descriptor = descriptor;
            EndPoint = new IPEndPoint(IPAddress.Loopback, reservedPort);
            _startupDuration = TimeSpan.Zero;
            _onWorkerStarted = onWorkerStarted;
            _onWorkerExited = onWorkerExited;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public override string Id { get; }

        public override string DisplayName { get; }

        public override DesiredWorkerDescriptor Descriptor { get; }

        public override IPEndPoint EndPoint { get; }

        public override TimeSpan StartupDuration => _startupDuration;

        private async Task BackgroundLoopAsync()
        {
            var configurationInfo = Descriptor.Configuration == "DebugGame" ? $"-{Descriptor.Platform}-DebugGame" : string.Empty;

            var editorPath = Path.Combine(Descriptor.EnginePath!, "Engine", "Binaries", Descriptor.Platform, $"UnrealEditor{configurationInfo}-Cmd" + (OperatingSystem.IsWindows() ? ".exe" : string.Empty));

            var projectRoot = Path.GetDirectoryName(Descriptor.UProjectPath)!;

            var logPath = Path.Combine(projectRoot, "Saved", "Logs", $"Worker_{Descriptor.Platform}_{Id}.log");

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
                $"-SessionId={Id.ToString().Replace("-", "").Replace("{", "").Replace("}", "").ToUpperInvariant()}",
                $"-SessionName={DisplayName}",
                $"-SessionOwner={Environment.UserName}",
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
                _cancellationTokenSource.Token));

            var didFireExit = false;
            try
            {
                // Wait for startup.
                do
                {
                    if (automationTask.IsCanceled ||
                        automationTask.IsFaulted)
                    {
                        _logger.LogWarning($"[{Id}] Executable exited unexpectedly with no exit code information");
                        didFireExit = true;
                        await _onWorkerExited(this, int.MinValue, await GrabCrashDataFromLogs(logPath));
                        return;
                    }
                    if (automationTask.IsCompletedSuccessfully)
                    {
                        _logger.LogWarning($"[{Id}] Executable exited unexpectedly with exit code {automationTask.Result}");
                        didFireExit = true;
                        await _onWorkerExited(this, automationTask.Result, await GrabCrashDataFromLogs(logPath));
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
                    catch (IOException ex) when (ex.Message.Contains("An existing connection was forcibly closed by the remote host."))
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
                await _onWorkerStarted(this);

                // Wait until DisposeAsync is called.
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // @note: This will throw, but DisposeAsync will eat the exception.
                    await Task.Delay(1000, _cancellationTokenSource.Token);
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
                    await automationTask;
                }
                catch
                {
                }
                if (!didFireExit)
                {
                    if (automationTask.IsCanceled ||
                        automationTask.IsFaulted)
                    {
                        _logger.LogTrace($"[{Id}] Executable exited with no exit code information");
                        await _onWorkerExited(this, int.MinValue, await GrabCrashDataFromLogs(logPath));
                    }
                    if (automationTask.IsCompletedSuccessfully)
                    {
                        _logger.LogTrace($"[{Id}] Executable exited with exit code {automationTask.Result}");
                        await _onWorkerExited(this, automationTask.Result, await GrabCrashDataFromLogs(logPath));
                    }
                }
            }
        }

        private async Task<IWorkerCrashData?> GrabCrashDataFromLogs(string logPath)
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
                            var line = await reader.ReadLineAsync();
                            if (line!.Contains("=== Critical error: ==="))
                            {
                                foundCrash = true;
                            }
                            if (foundCrash)
                            {
                                crashLines.Add(line.Substring(line.IndexOf("Error:") + "Error:".Length).Trim());
                            }
                            if (line.Contains("end: stack for UAT"))
                            {
                                foundCrash = false;
                            }
                        }
                    }
                    if (foundCrash)
                    {
                        return new LocalWorkerCrashData(string.Join("\n", crashLines));
                    }
                    return null;
                }
                catch (IOException ex) when (ex.Message.Contains("used by another process"))
                {
                    // Still waiting on Unreal to finish with the log file.
                    await Task.Delay(500);
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
                    await _backgroundTask;
                }
                catch (OperationCanceledException)
                {
                }
            }
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
