namespace Redpoint.Uefs.Commands.Mount
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Grpc.Core;
    using Redpoint.GrpcPipes;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Protocol;

    internal class ObservableOperation<TRequest, TResponse> : ITaskBasedProgress, IGitFetchBasedProgress, IByteBasedProgress
    {
        private readonly IRetryableGrpc _retryableGrpc;
        private readonly Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<TResponse>> _call;
        private readonly Func<TResponse, PollingResponse> _getPollingResponse;
        private readonly TRequest _request;
        private readonly TimeSpan _idleTimeout;
        private readonly CancellationToken _cancellationToken;
        private readonly ITaskBasedMonitor _taskMonitor;
        private bool _started;

        public ObservableOperation(
            IRetryableGrpc retryableGrpc,
            IMonitorFactory monitorFactory,
            Func<TRequest, Metadata?, DateTime?, CancellationToken, AsyncServerStreamingCall<TResponse>> call,
            Func<TResponse, PollingResponse> getPollingResponse,
            TRequest request,
            TimeSpan idleTimeout,
            CancellationToken cancellationToken)
        {
            _retryableGrpc = retryableGrpc;
            _call = call;
            _getPollingResponse = getPollingResponse;
            _request = request;
            _idleTimeout = idleTimeout;
            _cancellationToken = cancellationToken;
            _taskMonitor = monitorFactory.CreateTaskBasedMonitor();

            GitFetchBasedMonitor = monitorFactory.CreateGitFetchBasedMonitor();
            ByteBasedMonitor = monitorFactory.CreateByteBasedMonitor();

            CurrentStatus = PollingResponseStatus.Starting;
            CurrentTaskStartTime = DateTimeOffset.UtcNow;
        }

        public PollingResponseStatus CurrentStatus { get; private set; }

        public string CurrentTaskStatus => CurrentStatus.ToDisplayString();

        public DateTimeOffset CurrentTaskStartTime { get; private set; }

        public int? CurrentTaskIndex { get; private set; }

        public int? TotalTasks { get; private set; }

        public string? TaskAdditionalInfoSuffix { get; private set; }

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

        public async Task<TResponse> RunAndWaitForCompleteAsync()
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
                    var pollingResponse = _getPollingResponse(entry);

                    if (!string.IsNullOrWhiteSpace(pollingResponse.Err))
                    {
                        throw new InvalidOperationException($"Failed to mount via UEFS: {pollingResponse.Err}");
                    }

                    if (pollingResponse.Status == PollingResponseStatus.Pulling)
                    {
                        switch (pollingResponse.Type)
                        {
                            case PollingResponseType.Git:
                                {
                                    CurrentStatus = pollingResponse.Status;
                                    if (!gotInitialChange)
                                    {
                                        CurrentTaskStartTime = DateTimeOffset.UtcNow;
                                        gotInitialChange = true;
                                    }
                                    GitFetchBasedProgress = this;
                                    ByteBasedProgress = null;

                                    CurrentTaskIndex = null;
                                    TotalTasks = null;
                                    TaskAdditionalInfoSuffix = null;

                                    Position = 0;
                                    Length = 0;

                                    if (pollingResponse.GitTotalObjects > 0)
                                    {
                                        TotalObjects = pollingResponse.GitTotalObjects;
                                        ReceivedObjects = pollingResponse.GitReceivedObjects;
                                        ReceivedBytes = pollingResponse.GitReceivedBytes;
                                        IndexedObjects = pollingResponse.GitIndexedObjects;
                                    }
                                    else
                                    {
                                        TotalObjects = null;
                                        ReceivedObjects = null;
                                        ReceivedBytes = null;
                                        IndexedObjects = null;
                                    }
                                    FetchContext = pollingResponse.GitSlowFetch ? "(using slow libgit2)" : string.Empty;
                                    ServerProgressMessage = pollingResponse.GitServerProgressMessage;
                                    IndexingProgressWeight = pollingResponse.GitSlowFetch ? 1.0 : null;
                                }
                                break;
                            case PollingResponseType.Package:
                                {
                                    CurrentStatus = pollingResponse.Status;
                                    if (!gotInitialChange)
                                    {
                                        CurrentTaskStartTime = DateTimeOffset.UtcNow;
                                        gotInitialChange = true;
                                    }
                                    GitFetchBasedProgress = null;
                                    ByteBasedProgress = this;

                                    CurrentTaskIndex = null;
                                    TotalTasks = null;
                                    TaskAdditionalInfoSuffix = null;

                                    Position = pollingResponse.Position;
                                    Length = pollingResponse.Length;

                                    TotalObjects = null;
                                    ReceivedObjects = null;
                                    ReceivedBytes = null;
                                    IndexedObjects = null;
                                    FetchContext = null;
                                    ServerProgressMessage = null;
                                    IndexingProgressWeight = null;
                                }
                                break;
                            case PollingResponseType.Verify:
                                {
                                    CurrentStatus = pollingResponse.Status;
                                    if (!gotInitialChange)
                                    {
                                        CurrentTaskStartTime = DateTimeOffset.UtcNow;
                                        gotInitialChange = true;
                                    }
                                    GitFetchBasedProgress = null;
                                    ByteBasedProgress = this;

                                    CurrentTaskIndex = pollingResponse.VerifyPackageIndex + 1;
                                    TotalTasks = pollingResponse.VerifyPackageTotal;
                                    if (pollingResponse.VerifyChunksFixed == 1)
                                    {
                                        TaskAdditionalInfoSuffix = "(1 chunk fixed so far)";
                                    }
                                    else if (pollingResponse.VerifyChunksFixed > 1)
                                    {
                                        TaskAdditionalInfoSuffix = $"({pollingResponse.VerifyChunksFixed} chunks fixed so far)";
                                    }

                                    Position = pollingResponse.Position;
                                    Length = pollingResponse.Length;

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
                        CurrentStatus = pollingResponse.Status;
                        GitFetchBasedProgress = null;
                        ByteBasedProgress = null;
                    }

                    if (pollingResponse.Complete)
                    {
                        return entry;
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
