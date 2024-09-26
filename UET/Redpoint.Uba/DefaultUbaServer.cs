namespace Redpoint.Uba
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.ProcessExecution;
    using Redpoint.Uba.Native;
    using System;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Runtime.ExceptionServices;
    using System.Runtime.InteropServices;
    using System.Runtime.Versioning;
    using System.Threading;
    using System.Threading.Tasks;

    internal partial class DefaultUbaServer : IUbaServer
    {
        private readonly ILogger<DefaultUbaServer> _logger;
        private readonly IProcessArgumentParser _processArgumentParser;
        private readonly IProcessExecutor _localProcessExecutor;
        private readonly nint _ubaLogger;
        private readonly CancellationTokenSource _localWorkerCancellationTokenSource;
        private nint _server;
        private nint _storageServer;
        private nint _sessionServerCreateInfo;
        private nint _sessionServer;
        private readonly ConcurrentDictionary<ulong, bool> _returnedProcesses;
        private readonly TerminableAwaitableConcurrentQueue<UbaProcessDescriptor> _processQueue;
        private readonly Task[] _localWorkerTasks;
        private readonly SessionServer_RemoteProcessAvailableCallback _onRemoteProcessAvailableDelegate;
        private readonly SessionServer_RemoteProcessReturnedCallback _onRemoteProcessReturnedDelegate;
        private long _processesPendingInQueue;
        private long _processesExecutingLocally;
        private long _processesExecutingRemotely;

        #region Library Imports

        static DefaultUbaServer()
        {
            UbaNative.ThrowIfNotInitialized();
        }

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroyServer(
            nint server);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        [return: MarshalAs(UnmanagedType.I1)]
        private static partial bool Server_AddClient(
            nint server,
            string ip,
            int port,
            nint crypto);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint CreateStorageServer(
            nint server,
            string rootDir,
            ulong casCapacityBytes,
            [MarshalAs(UnmanagedType.I1)] bool storeCompressed,
            nint logger,
            string zone);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroyStorageServer(nint storageServer);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint CreateSessionServerCreateInfo(
            nint storageServer,
            nint server,
            nint logger,
            string rootDir,
            string traceOutputFile,
            [MarshalAs(UnmanagedType.I1)] bool disableCustomAllocator,
            [MarshalAs(UnmanagedType.I1)] bool launchVisualizer,
            [MarshalAs(UnmanagedType.I1)] bool resetCas,
            [MarshalAs(UnmanagedType.I1)] bool writeToDisk,
            [MarshalAs(UnmanagedType.I1)] bool detailedTrace,
            [MarshalAs(UnmanagedType.I1)] bool allowWaitOnMem,
            [MarshalAs(UnmanagedType.I1)] bool allowKillOnMem);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroySessionServerCreateInfo(nint sessionServerCreateInfo);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint CreateSessionServer(
            nint sessionServerCreateInfo);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroySessionServer(
            nint sessionServer);

        private delegate void ExitCallback(nint userData, nint handle);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint CreateProcessStartInfo(
            string filePath,
            string arguments,
            string workingDirectory,
            string description,
            uint priorityClass,
            ulong outputStatsThresholdMs,
            [MarshalAs(UnmanagedType.I1)] bool trackInputs,
            string logFile,
            ExitCallback? exit);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroyProcessStartInfo(
            nint processStartInfo);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint SessionServer_RunProcessRemote(
            nint sessionServer,
            nint processStartInfo,
            float weight,
            byte[]? knownInputs,
            uint knownInputsCount);

        private delegate void SessionServer_RemoteProcessAvailableCallback(nint userData);
        private delegate void SessionServer_RemoteProcessReturnedCallback(nint processHash, nint userData);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void SessionServer_SetRemoteProcessAvailable(
            nint sessionServer,
            SessionServer_RemoteProcessAvailableCallback returned,
            nint userData);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void SessionServer_SetRemoteProcessReturned(
            nint sessionServer,
            SessionServer_RemoteProcessReturnedCallback returned,
            nint userData);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial ulong ProcessHandle_GetHash(nint process);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial uint ProcessHandle_GetExitCode(nint process);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint ProcessHandle_GetExecutingHost(nint process);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial nint ProcessHandle_GetLogLine(nint process, uint index);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void ProcessHandle_Cancel(nint process, [MarshalAs(UnmanagedType.I1)] bool terminate);

        [LibraryImport("UbaHost", StringMarshalling = StringMarshalling.Custom, StringMarshallingCustomType = typeof(UbaStringMarshaller))]
        [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
        private static partial void DestroyProcessHandle(nint process);

        #endregion

        public DefaultUbaServer(
            ILogger<DefaultUbaServer> logger,
            IProcessArgumentParser processArgumentParser,
            IProcessExecutor localProcessExecutor,
            nint ubaLogger,
            nint server,
            string rootStorageDirectoryPath,
            string ubaTraceFilePath)
        {
            _logger = logger;
            _processArgumentParser = processArgumentParser;
            _localProcessExecutor = localProcessExecutor;
            _ubaLogger = ubaLogger;
            _localWorkerCancellationTokenSource = new CancellationTokenSource();

            _server = server;

            _storageServer = CreateStorageServer(
                _server,
                rootStorageDirectoryPath,
                40uL * 1000 * 1000 * 1000,
                false,
                ubaLogger,
                string.Empty);
            if (_storageServer == nint.Zero)
            {
                ReleaseNativeResources();
                throw new InvalidOperationException("Unable to create the UBA storage server!");
            }

            _sessionServerCreateInfo = CreateSessionServerCreateInfo(
                _storageServer,
                _server,
                ubaLogger,
                rootStorageDirectoryPath,
                ubaTraceFilePath,
                disableCustomAllocator: false,
                launchVisualizer: false,
                resetCas: false,
                writeToDisk: true,
                detailedTrace: false,
                allowWaitOnMem: true,
                allowKillOnMem: false);
            if (_sessionServerCreateInfo == nint.Zero)
            {
                ReleaseNativeResources();
                throw new InvalidOperationException("Unable to create the UBA session server creation info!");
            }

            _sessionServer = CreateSessionServer(_sessionServerCreateInfo);
            if (_sessionServer == nint.Zero)
            {
                ReleaseNativeResources();
                throw new InvalidOperationException("Unable to create the UBA session server!");
            }

            _returnedProcesses = new ConcurrentDictionary<ulong, bool>();
            _processQueue = new TerminableAwaitableConcurrentQueue<UbaProcessDescriptor>();
            _localWorkerTasks = Enumerable.Range(0, Environment.ProcessorCount / 2)
                .Select(x => Task.Run(LocalProcessWorkerLoop))
                .ToArray();

            _onRemoteProcessAvailableDelegate = OnRemoteProcessAvailable;
            _onRemoteProcessReturnedDelegate = OnRemoteProcessReturned;
            SessionServer_SetRemoteProcessAvailable(_sessionServer, _onRemoteProcessAvailableDelegate, nint.Zero);
            SessionServer_SetRemoteProcessReturned(_sessionServer, _onRemoteProcessReturnedDelegate, nint.Zero);
        }

        public bool AddRemoteAgent(string ip, int port)
        {
            return Server_AddClient(_server, ip, port, nint.Zero);
        }

        public long ProcessesPendingInQueue => Interlocked.Read(ref _processesPendingInQueue);
        public long ProcessesExecutingLocally => Interlocked.Read(ref _processesExecutingLocally);
        public long ProcessesExecutingRemotely => Interlocked.Read(ref _processesExecutingRemotely);

        private void OnRemoteProcessAvailable(nint userData)
        {
            // Start a background task to queue a remote process (since this function can't be async).
            _ = Task.Run(async () =>
            {
                // Before we attempt to actually dequeue, check that there is something to dequeue, since UBA
                // frequently calls OnRemoteProcessAvailable whileever slots are free (and not just when a slot
                // opens up).
                if (Interlocked.Read(ref _processesPendingInQueue) == 0)
                {
                    return;
                }

                _logger.LogInformation("Received OnRemoteProcessAvailable, pulling next available process to run...");

                // Grab the next process to run.
                var descriptor = await _processQueue.DequeueAsync(_localWorkerCancellationTokenSource.Token).ConfigureAwait(false);
                Interlocked.Decrement(ref _processesPendingInQueue);
                Interlocked.Increment(ref _processesExecutingRemotely);

                // Run the process remotely.
                _logger.LogInformation($"Got process to run '{descriptor.ProcessSpecification.FilePath}'...");
                var isRequeued = false;
                try
                {
                    // Create the gate that we can wait on until the process exits.
                    var exitedGate = new Gate();
                    ExitCallback exited = (nint userdata, nint handle) =>
                    {
                        exitedGate.Open();
                    };

                    // Create the process start info.
                    var processStartInfo = CreateProcessStartInfo(
                        descriptor.ProcessSpecification.FilePath,
                        _processArgumentParser.JoinArguments(descriptor.ProcessSpecification.Arguments),
                        descriptor.ProcessSpecification.WorkingDirectory ?? Environment.CurrentDirectory,
                        $"Redpoint.Uba: {descriptor.ProcessSpecification.FilePath} {_processArgumentParser.JoinArguments(descriptor.ProcessSpecification.Arguments)}",
                        (uint)ProcessPriorityClass.Normal,
                        int.MaxValue,
                        true,
                        string.Empty,
                        exited);
                    if (processStartInfo == nint.Zero)
                    {
                        throw new InvalidOperationException("Unable to create UBA process start info!");
                    }

                    // Process and log tracking that needs to be shared between try and finally blocks.
                    var process = nint.Zero;
                    uint logIndex = 0;
                    var getTargetLogLine = (uint targetLogIndex) =>
                    {
                        var ptr = ProcessHandle_GetLogLine(process, targetLogIndex);
                        if (ptr == nint.Zero)
                        {
                            return null;
                        }
                        if (OperatingSystem.IsWindows())
                        {
                            return Marshal.PtrToStringUni(ptr);
                        }
                        else
                        {
                            return Marshal.PtrToStringUTF8(ptr);
                        }
                    };
                    var getExecutingHost = () =>
                    {
                        var ptr = ProcessHandle_GetExecutingHost(process);
                        if (ptr == nint.Zero)
                        {
                            return null;
                        }
                        if (OperatingSystem.IsWindows())
                        {
                            return Marshal.PtrToStringUni(ptr);
                        }
                        else
                        {
                            return Marshal.PtrToStringUTF8(ptr);
                        }
                    };
                    var flushLogLines = () =>
                    {
                        if (process != nint.Zero)
                        {
                            var nextLogLine = getTargetLogLine(logIndex);
                            while (nextLogLine != null)
                            {
                                logIndex++;
                                if (descriptor.CaptureSpecification.InterceptStandardOutput)
                                {
                                    descriptor.CaptureSpecification.OnReceiveStandardOutput(nextLogLine);
                                }
                                else
                                {
                                    Console.WriteLine(nextLogLine);
                                }
                                nextLogLine = getTargetLogLine(logIndex);
                            }
                        }
                    };

                    // try/finally to ensure we release native resources when finished.
                    var isCancelled = false;
                    var isComplete = false;
                    ulong processHash = 0;
                    var releaseProcessResources = (bool forceCancel) =>
                    {
                        if (process != nint.Zero)
                        {
                            if (!isComplete && !isCancelled && (forceCancel || descriptor.CancellationToken.IsCancellationRequested))
                            {
                                ProcessHandle_Cancel(process, true);
                                isCancelled = true;
                            }
                            DestroyProcessHandle(process);
                            _returnedProcesses.Remove(processHash, out _);
                            process = nint.Zero;
                        }
                    };
                    try
                    {
                        // Check for cancellation.
                        descriptor.CancellationToken.ThrowIfCancellationRequested();

                        // Run the process remotely.
                        process = SessionServer_RunProcessRemote(
                            _sessionServer,
                            processStartInfo,
                            1.0f,
                            null,
                            0);
                        if (process == nint.Zero)
                        {
                            throw new InvalidOperationException("Unable to create UBA remote process!");
                        }
                        processHash = ProcessHandle_GetHash(process);

                        _logger.LogInformation($"Remote process '{descriptor.ProcessSpecification.FilePath}' is now running...");

                        // While we wait for the exit gate to open, poll for log lines.
                        while (!exitedGate.Opened &&
                            !_returnedProcesses.ContainsKey(processHash) &&
                            !descriptor.CancellationToken.IsCancellationRequested)
                        {
                            // Check for cancellation.
                            descriptor.CancellationToken.ThrowIfCancellationRequested();

                            // Flush all available log lines.
                            flushLogLines();

                            // Continue waiting for the process to exit.
                            await Task.Delay(200, descriptor.CancellationToken).ConfigureAwait(false);
                        }

                        // Get the exit code and mark as completed if the command finished running.
                        int? exitCode = null;
                        if (!_returnedProcesses.ContainsKey(processHash))
                        {
                            exitCode = (int)ProcessHandle_GetExitCode(process);
                            isComplete = true;
                        }

                        // Check if we should requeue this process.
                        if (!exitCode.HasValue /* Returned by remote agent */ ||
                            (exitCode.HasValue && exitCode == 9006) /* Known retry code when running cmd.exe via remote agent */)
                        {
                            _logger.LogInformation($"Remote process '{descriptor.ProcessSpecification.FilePath}' was returned to the queue, scheduling for local execution...");

                            // Prefer to run this command locally now.
                            descriptor.PreferRemote = false;

                            // Cancel and release remote process.
                            releaseProcessResources(true);

                            // Push this process back into the queue for local execution.
                            _processQueue.Enqueue(descriptor);
                            isRequeued = true;
                            return;
                        }

                        // Otherwise, return the exit code.
                        isComplete = true;
                        descriptor.ExitCode = exitCode!.Value;
                        return;
                    }
                    finally
                    {
                        flushLogLines();
                        releaseProcessResources(false);
                        DestroyProcessStartInfo(processStartInfo);
                    }
                }
                catch (Exception ex)
                {
                    if (!isRequeued)
                    {
                        descriptor.ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                    }
                }
                finally
                {
                    if (!isRequeued)
                    {
                        descriptor.CompletionGate.Open();
                    }
                    else
                    {
                        Interlocked.Increment(ref _processesPendingInQueue);
                    }
                    Interlocked.Decrement(ref _processesExecutingRemotely);
                }
            });
        }

        private void OnRemoteProcessReturned(nint process, nint userData)
        {
            _returnedProcesses.AddOrUpdate((ulong)process.ToInt64(), true, (_, _) => true);
        }

        private async Task LocalProcessWorkerLoop()
        {
            do
            {
                // Grab the next process to run.
                var descriptor = await _processQueue.DequeueAsync(_localWorkerCancellationTokenSource.Token).ConfigureAwait(false);
                if (descriptor.PreferRemote &&
                    (DateTimeOffset.UtcNow - descriptor.DateQueuedUtc).TotalSeconds < 30)
                {
                    // If this process prefers remote execution, and it hasn't been sitting in the queue for
                    // at least 30 seconds, requeue it and try again.
                    _processQueue.Enqueue(descriptor);
                    await Task.Delay(200, _localWorkerCancellationTokenSource.Token).ConfigureAwait(false);
                    continue;
                }
                Interlocked.Decrement(ref _processesPendingInQueue);
                Interlocked.Increment(ref _processesExecutingLocally);

                // Run the process locally.
                try
                {
                    descriptor.ExitCode = await _localProcessExecutor.ExecuteAsync(
                        descriptor.ProcessSpecification,
                        descriptor.CaptureSpecification,
                        descriptor.CancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    descriptor.ExceptionDispatchInfo = ExceptionDispatchInfo.Capture(ex);
                }
                finally
                {
                    descriptor.CompletionGate.Open();
                    Interlocked.Decrement(ref _processesExecutingLocally);
                }
            }
            while (true);
        }

        public async Task<int> ExecuteAsync(
            ProcessSpecification processSpecification,
            ICaptureSpecification captureSpecification,
            CancellationToken cancellationToken)
        {
            // If the capture specification redirects standard input, this is
            // an invalid request.
            if (captureSpecification.InterceptStandardInput)
            {
                throw new ArgumentException("Standard input can not be intercepted for UBA remote processes.", nameof(captureSpecification));
            }

            // Push the request into the queue.
            var descriptor = new UbaProcessDescriptor
            {
                ProcessSpecification = processSpecification,
                CaptureSpecification = captureSpecification,
                CancellationToken = cancellationToken,
                DateQueuedUtc = DateTimeOffset.UtcNow,
                PreferRemote = processSpecification is UbaProcessSpecification ubaProcessSpecification && ubaProcessSpecification.PreferRemote,
                CompletionGate = new Gate(),
            };
            _processQueue.Enqueue(descriptor);
            Interlocked.Increment(ref _processesPendingInQueue);

            // Wait for the gate to be opened.
            await descriptor.CompletionGate.WaitAsync(cancellationToken).ConfigureAwait(false);

            // If an exception was thrown, rethrow it now.
            if (descriptor.ExceptionDispatchInfo != null)
            {
                descriptor.ExceptionDispatchInfo.Throw();
            }

            // Return the exit code.
            return descriptor.ExitCode;
        }

        private void ReleaseNativeResources()
        {
            if (_sessionServer != nint.Zero)
            {
                DestroySessionServer(_sessionServer);
                _sessionServer = nint.Zero;
            }
            if (_sessionServerCreateInfo != nint.Zero)
            {
                DestroySessionServerCreateInfo(_sessionServerCreateInfo);
                _sessionServerCreateInfo = nint.Zero;
            }
            if (_storageServer != nint.Zero)
            {
                DestroyStorageServer(_storageServer);
                _storageServer = nint.Zero;
            }
            if (_server != nint.Zero)
            {
                DestroyServer(_server);
                _server = nint.Zero;
            }
        }

        ~DefaultUbaServer()
        {
            ReleaseNativeResources();
            _localWorkerCancellationTokenSource.Cancel();
        }

        public async ValueTask DisposeAsync()
        {
            _localWorkerCancellationTokenSource.Cancel();
            try
            {
                foreach (var task in _localWorkerTasks)
                {
                    await task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            ReleaseNativeResources();
            _localWorkerCancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
