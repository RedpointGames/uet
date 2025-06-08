﻿namespace Redpoint.Uet.Automation.Worker.Local
{
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Uat;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class LocalGauntletWorker : LocalWorker, ICaptureSpecification
    {
        private readonly ILogger<LocalGauntletWorker> _logger;
        private readonly IProcessExecutor _processExecutor;
        private readonly IUATExecutor _uatExecutor;
        private readonly ILoopbackPortReservation _portReservation;

        private readonly OnWorkerStarted _onWorkerStarted;
        private readonly OnWorkerExited _onWorkerExited;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _backgroundTask;
        private TimeSpan _startupDuration;

        public LocalGauntletWorker(
            ILogger<LocalGauntletWorker> logger,
            IProcessExecutor processExecutor,
            IUATExecutor uatExecutor,
            string id,
            string displayName,
            ILoopbackPortReservation portReservation,
            DesiredWorkerDescriptor descriptor,
            OnWorkerStarted onWorkerStarted,
            OnWorkerExited onWorkerExited)
        {
            _logger = logger;
            _processExecutor = processExecutor;
            _uatExecutor = uatExecutor;

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
            var arguments = new List<string>
            {
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
                // The engine will check for UObject leaks inside world memory and force a crash if it detects them. However, these are unrelated to
                // automation tests (which don't operate in the world for the most part), so we want to avoid these crashes.
                "-ini:Engine:[Core.Log]:LogEditorServer=NoLogging",
                // Configure messaging transports so that our C# libraries can talk
                // to this instance.
                "-EnablePlugins=TcpMessaging",
                "-DisablePlugins=UdpMessaging",
                "-ini:Engine:[/Script/TcpMessaging.TcpMessagingSettings]:EnableTransport=True",
                $"-ini:Engine:[/Script/TcpMessaging.TcpMessagingSettings]:ListenEndpoint={_portReservation.EndPoint}",
                "-ini:Engine:[/Script/UdpMessaging.UdpMessagingSettings]:EnabledByDefault=False",
                "-ini:Engine:[/Script/UdpMessaging.UdpMessagingSettings]:EnableTransport=False",
                // Disable Zen cache, as it does not work under SYSTEM.
                "-DDC=NoZenLocalFallback"
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

            // @todo: We need to figure out how we obtain devices for this.

            var gauntletOutputPath = Descriptor.WorkerLogsPath != null
                ? Path.Combine(Descriptor.WorkerLogsPath, $"GauntletOutput")
                : Path.Combine(Path.GetDirectoryName(Descriptor.UProjectPath)!, "Saved", "GauntletOutput");

            // Start the process in the background.
            var st = Stopwatch.StartNew();
            var automationTask = Task.Run(async () => await _uatExecutor.ExecuteAsync(
                Descriptor.EnginePath,
                new UATSpecification
                {
                    Command = "RunUnreal",
                    Arguments = new LogicalProcessArgument[]
                    {
                        "-noP4",
                        $@"-project={Descriptor.UProjectPath}",
                        $"-configuration={Descriptor.Configuration}",
                        $"-build=local",
                        $@"-tempdir={gauntletOutputPath}",
                        $@"-args={string.Join(" ", arguments)}",
                    },
                },
                this,
                Array.Empty<string>(),
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
                        await _onWorkerExited(this, automationTask.Result, null).ConfigureAwait(false);
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
                    catch (IOException ex) when (ex.Message.Contains("An existing connection was forcibly closed by the remote host.", StringComparison.OrdinalIgnoreCase))
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

                // Wait until DisposeAsync is called.
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // @note: This will throw, but DisposeAsync will eat the exception.
                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
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
                        await _onWorkerExited(this, automationTask.Result, null).ConfigureAwait(false);
                    }
                }
            }
        }

        /*
        private async Task<IWorkerCrashData?> GrabCrashDataFromLogs(string logPath)
        {
            var foundCrash = false;
            var crashLines = new List<string>();
            foreach (var line in await File.ReadAllLinesAsync(logPath))
            {
                if (line.Contains("=== Critical error: ==="))
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
            if (foundCrash)
            {
                return new LocalWorkerCrashData(string.Join("\n", crashLines));
            }
            return null;
        }
        */

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
