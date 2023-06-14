namespace Redpoint.UET.Workspace
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Protocol;

    internal class ObservableMountOperation<TRequest> : ITaskBasedProgress, IGitFetchBasedProgress, IByteBasedProgress
    {
        private readonly IRetryableGrpc _retryableGrpc;
        private readonly Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<MountResponse>> _call;
        private readonly TRequest _request;
        private readonly TimeSpan _idleTimeout;
        private readonly CancellationToken _cancellationToken;
        private readonly ITaskBasedMonitor _taskMonitor;
        private bool _started;

        public ObservableMountOperation(
            IRetryableGrpc retryableGrpc,
            IMonitorFactory monitorFactory,
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<MountResponse>> call,
            TRequest request,
            TimeSpan idleTimeout,
            CancellationToken cancellationToken)
        {
            _retryableGrpc = retryableGrpc;
            _call = call;
            _request = request;
            _idleTimeout = idleTimeout;
            _cancellationToken = cancellationToken;
            _taskMonitor = monitorFactory.CreateTaskBasedMonitor();

            GitFetchBasedMonitor = monitorFactory.CreateGitFetchBasedMonitor();
            ByteBasedMonitor = monitorFactory.CreateByteBasedMonitor();

            CurrentTaskStatus = "starting";
            CurrentTaskStartTime = DateTimeOffset.UtcNow;
        }

        public string CurrentTaskStatus { get; private set; }

        public DateTimeOffset CurrentTaskStartTime { get; private set; }

        public int? CurrentTaskIndex { get; private set; }

        public int? TotalTasks { get; private set; }

        public IByteBasedProgress? ByteBasedProgress { get; private set; }

        public IByteBasedMonitor? ByteBasedMonitor { get; private set; }

        public IGitFetchBasedProgress? GitFetchBasedProgress { get; private set; }

        public IGitFetchBasedMonitor? GitFetchBasedMonitor { get; private set; }

        public int? TotalObjects { get; private set; }

        public int? ReceivedObjects { get; private set; }

        public long? ReceivedBytes { get; private set; }

        public int? IndexedObjects { get; private set; }

        public string? FetchContext { get; private set; }

        public string? ServerProgressMessage { get; private set; }

        public double? IndexingProgressWeight { get; private set; }

        public long Position { get; private set; }

        public long Length { get; private set; }

        private class DidOutput
        {
            public bool Did;
        }

        public async Task<string> RunAndWaitForMountIdAsync()
        {
            if (_started)
            {
                throw new InvalidOperationException("ObservableMountOperations can't be re-used.");
            }

            _started = true;
            var gotInitialChange = false;

            var monitorCts = new CancellationTokenSource();
            var outputTrack = new DidOutput();
            var monitorTask = Task.Run(async () =>
            {
                var consoleWidth = 0;
                try
                {
                    consoleWidth = Console.BufferWidth;
                }
                catch { }

                await _taskMonitor.MonitorAsync(
                    this,
                    null,
                    (message, count) =>
                    {
                        if (consoleWidth != 0)
                        {
                            Console.Write($"\r                {message}".PadRight(consoleWidth));
                            outputTrack.Did = true;
                        }
                        else if (count % 50 == 0)
                        {
                            Console.WriteLine($"                {message}");
                            outputTrack.Did = true;
                        }
                    },
                    monitorCts.Token);
            });

            try
            {
                await foreach (var entry in _retryableGrpc.RetryableStreamingGrpcAsync(
                    _call,
                    _request,
                    new GrpcRetryConfiguration { RequestTimeout = _idleTimeout },
                    _cancellationToken))
                {
                    if (!string.IsNullOrWhiteSpace(entry.PollingResponse.Err))
                    {
                        throw new InvalidOperationException($"Failed to mount via UEFS: {entry.PollingResponse.Err}");
                    }

                    if (entry.PollingResponse.Status == "pulling")
                    {
                        switch (entry.PollingResponse.Type)
                        {
                            case PollingResponseType.Git:
                                {
                                    CurrentTaskStatus = entry.PollingResponse.Status;
                                    if (!gotInitialChange)
                                    {
                                        CurrentTaskStartTime = DateTimeOffset.UtcNow;
                                        gotInitialChange = true;
                                    }
                                    GitFetchBasedProgress = this;
                                    ByteBasedProgress = null;

                                    Position = 0;
                                    Length = 0;

                                    if (entry.PollingResponse.GitTotalObjects > 0)
                                    {
                                        TotalObjects = entry.PollingResponse.GitTotalObjects;
                                        ReceivedObjects = entry.PollingResponse.GitReceivedObjects;
                                        ReceivedBytes = entry.PollingResponse.GitReceivedBytes;
                                        IndexedObjects = entry.PollingResponse.GitIndexedObjects;
                                    }
                                    else
                                    {
                                        TotalObjects = null;
                                        ReceivedObjects = null;
                                        ReceivedBytes = null;
                                        IndexedObjects = null;
                                    }
                                    FetchContext = entry.PollingResponse.GitSlowFetch ? "(using slow libgit2)" : string.Empty;
                                    ServerProgressMessage = entry.PollingResponse.GitServerProgressMessage;
                                    IndexingProgressWeight = entry.PollingResponse.GitSlowFetch ? 1.0 : null;
                                }
                                break;
                            case PollingResponseType.Package:
                                {
                                    CurrentTaskStatus = entry.PollingResponse.Status;
                                    if (!gotInitialChange)
                                    {
                                        CurrentTaskStartTime = DateTimeOffset.UtcNow;
                                        gotInitialChange = true;
                                    }
                                    GitFetchBasedProgress = null;
                                    ByteBasedProgress = this;

                                    Position = entry.PollingResponse.Position;
                                    Length = entry.PollingResponse.Length;

                                    TotalObjects = null;
                                    ReceivedObjects = null;
                                    ReceivedBytes = null;
                                    IndexedObjects = null;
                                    FetchContext = null;
                                    ServerProgressMessage = null;
                                    IndexingProgressWeight = null;
                                }
                                break;
                        }
                    }
                    else
                    {
                        CurrentTaskStatus = entry.PollingResponse.Status;
                        GitFetchBasedProgress = null;
                        ByteBasedProgress = null;
                    }

                    if (entry.PollingResponse.Complete)
                    {
                        return entry.MountId;
                    }
                }

                throw new InvalidOperationException("UEFS did not return a complete response or error!");
            }
            finally
            {
                monitorCts.Cancel();
                try
                {
                    await monitorTask;
                }
                catch (OperationCanceledException)
                {
                }
                if (outputTrack.Did)
                {
                    var consoleWidth = 0;
                    try
                    {
                        consoleWidth = Console.BufferWidth;
                    }
                    catch { }
                    if (consoleWidth != 0)
                    {
                        Console.WriteLine();
                    }
                }
            }
        }
    }
}
