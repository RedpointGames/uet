﻿namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Google.Protobuf.Collections;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Diagnostics;
    using System.Globalization;
    using System.Threading.Tasks;

    internal class DefaultGraphExecutor : IGraphExecutor
    {
        private readonly ILogger<DefaultGraphExecutor> _logger;
        private readonly IToolSynchroniser _toolSynchroniser;
        private readonly IBlobSynchroniser _blobSynchroniser;

        public DefaultGraphExecutor(
            ILogger<DefaultGraphExecutor> logger,
            IToolSynchroniser toolSynchroniser,
            IBlobSynchroniser blobSynchroniser)
        {
            _logger = logger;
            _toolSynchroniser = toolSynchroniser;
            _blobSynchroniser = blobSynchroniser;
        }

        private class GraphExecutionInstance
        {
            public required ITaskApiWorkerPool WorkerPool;
            public long RemainingTasks;
            public readonly SemaphoreSlim QueuedTaskAvailableForScheduling = new SemaphoreSlim(0);
            public readonly ConcurrentQueue<GraphTask> QueuedTasksForScheduling = new ConcurrentQueue<GraphTask>();
            private CancellationTokenSource _cancellationTokenSource;
            public CancellationToken CancellationToken => _cancellationTokenSource.Token;
            public readonly List<Task> ScheduledExecutions = new List<Task>();
            public readonly SemaphoreSlim ScheduledExecutionsLock = new SemaphoreSlim(1);
            public readonly HashSet<GraphTask> ScheduledTasks = new HashSet<GraphTask>();
            public readonly HashSet<GraphTask> CompletedTasks = new HashSet<GraphTask>();
            public readonly HashSet<GraphTask> CancelledDueToUpstreamFailureTasks = new HashSet<GraphTask>();
            public readonly SemaphoreSlim CompletedAndScheduledTasksLock = new SemaphoreSlim(1);
            public bool IsCancelledDueToException { get; private set; }
            public string? ExceptionMessage { get; private set; }

            public GraphExecutionInstance(CancellationToken cancellationToken)
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            }

            internal void CancelDueToException(Exception ex)
            {
                IsCancelledDueToException = true;
                ExceptionMessage = ex.ToString();
                _cancellationTokenSource.Cancel();
            }
        }

        public async Task ExecuteGraphAsync(
            ITaskApiWorkerPool workerPool,
            Graph graph,
            JobBuildBehaviour buildBehaviour,
            GuardedResponseStream<JobResponse> responseStream,
            CancellationToken cancellationToken)
        {
            // Make sure there's at least one task. Empty graphs should not be passed
            // to this function.
            if (graph.Tasks.Count == 0)
            {
                throw new ArgumentException("There are no tasks defined in the graph.");
            }

            var graphStopwatch = Stopwatch.StartNew();

            // Track the state of this graph execution.
            var instance = new GraphExecutionInstance(cancellationToken)
            {
                WorkerPool = workerPool,
                RemainingTasks = graph.Tasks.Count,
            };

            // Schedule up all of the tasks that can be immediately
            // scheduled.
            foreach (var taskKv in graph.Tasks)
            {
                if (graph.TaskDependencies.WhatTargetDependsOn(taskKv.Value).Count == 0)
                {
                    instance.QueuedTasksForScheduling.Enqueue(taskKv.Value);
                    instance.QueuedTaskAvailableForScheduling.Release();
                    // @note: We don't take a lock here because nothing else will
                    // be accessing it yet.
                    instance.ScheduledTasks.Add(taskKv.Value);
                }
            }

            // At this point, if we don't have anything that can be
            // scheduled, then the execution can never make any progress.
            if (instance.QueuedTasksForScheduling.Count == 0)
            {
                throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    "No task described by the job XML was immediately schedulable."));
            }

            // Pull tasks off the queue until we have no tasks remaining.
            try
            {
                while (!instance.CancellationToken.IsCancellationRequested &&
                       instance.RemainingTasks > 0)
                {
                    // Get the next task to schedule. This queue only contains
                    // tasks whose dependencies have all passed.
                    await instance.QueuedTaskAvailableForScheduling.WaitAsync(
                        instance.CancellationToken);
                    if (Interlocked.Read(ref instance.RemainingTasks) == 0)
                    {
                        break;
                    }
                    if (!instance.QueuedTasksForScheduling.TryDequeue(out var task))
                    {
                        // This can happen during failures when we want this while loop
                        // to recheck RemainingTasks. Go back and fetch more tasks.
                        continue;
                    }

                    // Schedule up a background 
                    instance.ScheduledExecutions.Add(Task.Run(async () =>
                    {
                        var status = TaskCompletionStatus.TaskCompletionException;
                        var exitCode = 1;
                        var exceptionMessage = string.Empty;
                        var didStart = false;
                        var didComplete = false;
                        var taskStopwatch = new Stopwatch();
                        var skipEmitComplete = false;
                        var currentPhaseStopwatch = new Stopwatch();
                        var currentPhaseStartTimestamp = DateTimeOffset.UtcNow;
                        var currentPhase = TaskPhase.Initial;
                        var finalPhaseMetadata = new Dictionary<string, string>();
                        async Task SendPhaseChangeAsync(
                            TaskPhase newPhase,
                            IDictionary<string, string> previousPhaseMetadata)
                        {
                            currentPhase = newPhase;
                            currentPhaseStartTimestamp = DateTimeOffset.UtcNow;
                            var totalSecondsInToolSynchronisationPhase = currentPhaseStopwatch!.Elapsed.TotalSeconds;
                            currentPhaseStopwatch.Restart();
                            var phaseChange = new TaskPhaseChangeResponse
                            {
                                Id = task.GraphTaskSpec.Task.Name,
                                DisplayName = task.GraphTaskSpec.Task.Caption,
                                NewPhase = currentPhase,
                                NewPhaseStartTimeUtcTicks = currentPhaseStartTimestamp.UtcTicks,
                                TotalSecondsInPreviousPhase = totalSecondsInToolSynchronisationPhase
                            };
                            phaseChange.PreviousPhaseMetadata.Add(previousPhaseMetadata);
                            await responseStream.WriteAsync(new JobResponse
                            {
                                TaskPhaseChange = phaseChange,
                            });
                        }
                        try
                        {
                            try
                            {
                                // Try to get a local core first, since this will let us run remote
                                // tasks much faster.
                                IWorkerCoreRequest<ITaskApiWorkerCore>? localCoreRequest = null;
                                if (buildBehaviour != null && buildBehaviour.ForceRemotingForLocalWorker)
                                {
                                    _logger.LogTrace("Skipping fast local execution because this job requested forced remoting.");
                                }
                                else if (Debugger.IsAttached)
                                {
                                    _logger.LogTrace("Skipping fast local execution because a debugger is attached.");
                                }
                                else
                                {
                                    _logger.LogTrace("Trying to get a local core...");
                                    var localCoreTimeout = CancellationTokenSource.CreateLinkedTokenSource(
                                        new CancellationTokenSource(250).Token,
                                        instance.CancellationToken);
                                    try
                                    {
                                        localCoreRequest = await instance.WorkerPool.ReserveCoreAsync(
                                            CoreAllocationPreference.RequireLocal,
                                            localCoreTimeout.Token);
                                        _logger.LogTrace("Obtained a local core.");
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        // Could not get a local core fast enough.
                                        _logger.LogTrace("Unable to get a local core, potentially remoting instead.");
                                    }
                                }
                                try
                                {
                                    // Do descriptor generation based on the task.
                                    TaskDescriptor taskDescriptor;
                                    switch (task)
                                    {
                                        case DescribingGraphTask describingGraphTask:
                                            // Generate the task descriptor from the factory. This can take a while
                                            // if we're parsing preprocessor headers.
                                            var downstreamTasks = graph.TaskDependencies.WhatDependsOnTarget(task);
                                            var isLocalCoreCandidateThatCanRunLocally =
                                                localCoreRequest != null &&
                                                downstreamTasks.Count == 1;
                                            Stopwatch? prepareStopwatch = null;
                                            if (!string.IsNullOrWhiteSpace(describingGraphTask.TaskDescriptorFactory.PreparationOperationDescription) &&
                                                !isLocalCoreCandidateThatCanRunLocally)
                                            {
                                                prepareStopwatch = Stopwatch.StartNew();
                                                await responseStream.WriteAsync(new JobResponse
                                                {
                                                    TaskPreparing = new TaskPreparingResponse
                                                    {
                                                        Id = describingGraphTask.GraphTaskSpec.Task.Name,
                                                        DisplayName = describingGraphTask.GraphTaskSpec.Task.Caption,
                                                        OperationDescription = describingGraphTask.TaskDescriptorFactory.PreparationOperationDescription,
                                                    }
                                                });
                                            }
                                            describingGraphTask.TaskDescriptor = taskDescriptor = await describingGraphTask.TaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(
                                                task.GraphTaskSpec,
                                                isLocalCoreCandidateThatCanRunLocally,
                                                instance.CancellationToken);
                                            if (describingGraphTask.TaskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                !taskDescriptor.Remote.UseFastLocalExecution)
                                            {
                                                // We must hash the tools and blobs ahead of time while we don't
                                                // hold a reservation on a remote worker. We can't spend time hashing
                                                // data with a reservation open because remote workers will kick us for
                                                // being idle.
                                                await Task.WhenAll(
                                                    Task.Run(async () =>
                                                    {
                                                        describingGraphTask.ToolHashingResult = await _toolSynchroniser.HashToolAsync(
                                                            describingGraphTask.TaskDescriptor.Remote,
                                                            instance.CancellationToken);
                                                    }),
                                                    Task.Run(async () =>
                                                    {
                                                        describingGraphTask.BlobHashingResult = await _blobSynchroniser.HashInputBlobsAsync(
                                                            describingGraphTask.TaskDescriptor.Remote,
                                                            instance.CancellationToken);
                                                    }));
                                            }
                                            if (prepareStopwatch != null &&
                                                !isLocalCoreCandidateThatCanRunLocally)
                                            {
                                                await responseStream.WriteAsync(new JobResponse
                                                {
                                                    TaskPrepared = new TaskPreparedResponse
                                                    {
                                                        Id = task.GraphTaskSpec.Task.Name,
                                                        DisplayName = task.GraphTaskSpec.Task.Caption,
                                                        TotalSeconds = prepareStopwatch!.Elapsed.TotalSeconds,
                                                        OperationCompletedDescription = describingGraphTask.TaskDescriptorFactory.PreparationOperationCompletedDescription ?? string.Empty,
                                                    }
                                                });
                                            }
                                            if (isLocalCoreCandidateThatCanRunLocally)
                                            {
                                                // Shortcut into the downstream execution task.
                                                await FinishTaskAsync(
                                                    graph,
                                                    instance,
                                                    task,
                                                    TaskCompletionStatus.TaskCompletionSuccess,
                                                    false);
                                                task = downstreamTasks.First();
                                            }
                                            break;
                                        case FastExecutableGraphTask fastExecutableGraphTask:
                                            taskDescriptor = await fastExecutableGraphTask.TaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(
                                                fastExecutableGraphTask.GraphTaskSpec,
                                                localCoreRequest != null,
                                                instance.CancellationToken);
                                            if (taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                !taskDescriptor.Remote.UseFastLocalExecution)
                                            {
                                                // We must hash the tools and blobs ahead of time while we don't
                                                // hold a reservation on a remote worker. We can't spend time hashing
                                                // data with a reservation open because remote workers will kick us for
                                                // being idle.
                                                await Task.WhenAll(
                                                    Task.Run(async () =>
                                                    {
                                                        fastExecutableGraphTask.ToolHashingResult = await _toolSynchroniser.HashToolAsync(
                                                            taskDescriptor.Remote,
                                                            instance.CancellationToken);
                                                    }),
                                                    Task.Run(async () =>
                                                    {
                                                        fastExecutableGraphTask.BlobHashingResult = await _blobSynchroniser.HashInputBlobsAsync(
                                                            taskDescriptor.Remote,
                                                            instance.CancellationToken);
                                                    }));
                                            }
                                            break;
                                        case ExecutableGraphTask executableGraphTask:
                                            taskDescriptor = executableGraphTask.DescribingGraphTask.TaskDescriptor!;
                                            break;
                                        default:
                                            throw new NotSupportedException();
                                    }

                                    // Do execution based on the task.
                                    switch (task)
                                    {
                                        case DescribingGraphTask describingGraphTask:
                                            // No execution work to do for this task.
                                            if (describingGraphTask.TaskDescriptor == null ||
                                                (describingGraphTask.TaskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                 describingGraphTask.TaskDescriptor.Remote.UseFastLocalExecution))
                                            {
                                                exitCode = 1;
                                                status = TaskCompletionStatus.TaskCompletionException;
                                                didComplete = true;
                                                skipEmitComplete = false;
                                                exceptionMessage = "Resulting descriptor was not valid from describing task!";
                                            }
                                            else
                                            {
                                                exitCode = 0;
                                                status = TaskCompletionStatus.TaskCompletionSuccess;
                                                didComplete = true;
                                                skipEmitComplete = true;
                                            }
                                            break;
                                        case IRemotableGraphTask remotableGraphTask:
                                            {
                                                // Reserve a core from somewhere...
                                                _logger.LogTrace("Waiting for core reservation for task...");
                                                IWorkerCoreRequest<ITaskApiWorkerCore> coreRequest;
                                                if (localCoreRequest != null)
                                                {
                                                    coreRequest = localCoreRequest;
                                                    localCoreRequest = null; // So we don't call DisposeAsync twice on the same core.
                                                }
                                                else
                                                {
                                                    if (taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                        taskDescriptor.Remote.UseFastLocalExecution)
                                                    {
                                                        throw new InvalidOperationException("UseFastLocalExecution must not be set if we're not using a local core!");
                                                    }
                                                    coreRequest = await instance.WorkerPool.ReserveCoreAsync(
                                                        taskDescriptor.DescriptorCase != TaskDescriptor.DescriptorOneofCase.Remote
                                                            ? CoreAllocationPreference.RequireLocal
                                                            : CoreAllocationPreference.PreferRemote,
                                                        instance.CancellationToken);
                                                }
                                                await using (coreRequest)
                                                {
                                                    // We're now going to start doing the work for this task.
                                                    var core = await coreRequest.WaitForCoreAsync(CancellationToken.None);
                                                    _logger.LogTrace($"Got core reservation from: {core.WorkerMachineName} {core.WorkerCoreNumber}");
                                                    taskStopwatch.Start();
                                                    currentPhaseStopwatch.Start();
                                                    currentPhase = (
                                                        taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                        !taskDescriptor.Remote.UseFastLocalExecution)
                                                            ? TaskPhase.RemoteToolSynchronisation
                                                            : TaskPhase.TaskExecution;
                                                    await responseStream.WriteAsync(new JobResponse
                                                    {
                                                        TaskStarted = new TaskStartedResponse
                                                        {
                                                            Id = task.GraphTaskSpec.Task.Name,
                                                            DisplayName = task.GraphTaskSpec.Task.Caption,
                                                            WorkerMachineName = core.WorkerMachineName,
                                                            WorkerCoreNumber = core.WorkerCoreNumber,
                                                            InitialPhaseStartTimeUtcTicks = currentPhaseStartTimestamp.UtcTicks,
                                                            InitialPhase = currentPhase,
                                                        },
                                                    }, instance.CancellationToken);
                                                    didStart = true;

                                                    // Perform synchronisation for remote tasks.
                                                    if (taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                        !taskDescriptor.Remote.UseFastLocalExecution)
                                                    {
                                                        // Synchronise the tool and determine the hash to
                                                        // use for the actual request.
                                                        _logger.LogTrace($"{core.WorkerCoreUniqueAssignmentId}: Synchronising tool...");
                                                        var toolExecutionInfo = await _toolSynchroniser.SynchroniseToolAndGetXxHash64Async(
                                                            core,
                                                            remotableGraphTask.ToolHashingResult!,
                                                            instance.CancellationToken);
                                                        taskDescriptor.Remote.ToolExecutionInfo = toolExecutionInfo;

                                                        // Notify the client we're changing phases.
                                                        _logger.LogTrace($"{core.WorkerCoreUniqueAssignmentId}: Sending phase change to client...");
                                                        await SendPhaseChangeAsync(
                                                            TaskPhase.RemoteInputBlobSynchronisation,
                                                            new Dictionary<string, string>
                                                            {
                                                                { "tool.xxHash64", taskDescriptor.Remote.ToolExecutionInfo.ToolXxHash64.HexString() },
                                                                { "tool.executableName", taskDescriptor.Remote.ToolExecutionInfo.ToolExecutableName },
                                                            });

                                                        // Synchronise all of the input blobs.
                                                        _logger.LogTrace($"{core.WorkerCoreUniqueAssignmentId}: Synchronising {remotableGraphTask.BlobHashingResult!.ContentHashesToContent.Count} blobs...");
                                                        var inputBlobSynchronisation = await _blobSynchroniser.SynchroniseInputBlobsAsync(
                                                            core,
                                                            remotableGraphTask.BlobHashingResult!,
                                                            instance.CancellationToken);
                                                        taskDescriptor.Remote.InputsByBlobXxHash64 = inputBlobSynchronisation.Result;

                                                        // Notify the client we're changing phases.
                                                        _logger.LogTrace($"{core.WorkerCoreUniqueAssignmentId}: Sending phase change to client...");
                                                        await SendPhaseChangeAsync(
                                                            TaskPhase.TaskExecution,
                                                            new Dictionary<string, string>
                                                            {
                                                                {
                                                                    "inputBlobSync.elapsedUtcTicksHashingInputFiles",
                                                                    inputBlobSynchronisation.ElapsedUtcTicksHashingInputFiles.ToString(CultureInfo.InvariantCulture)
                                                                },
                                                                {
                                                                    "inputBlobSync.elapsedUtcTicksQueryingMissingBlobs",
                                                                    inputBlobSynchronisation.ElapsedUtcTicksQueryingMissingBlobs.ToString(CultureInfo.InvariantCulture)
                                                                },
                                                                {
                                                                    "inputBlobSync.elapsedUtcTicksTransferringCompressedBlobs",
                                                                    inputBlobSynchronisation.ElapsedUtcTicksTransferringCompressedBlobs.ToString(CultureInfo.InvariantCulture)
                                                                },
                                                                {
                                                                    "inputBlobSync.compressedDataTransferLength",
                                                                    inputBlobSynchronisation.CompressedDataTransferLength.ToString(CultureInfo.InvariantCulture)
                                                                },
                                                            });
                                                    }

                                                    // Execute the task on the core.
                                                    _logger.LogTrace($"{core.WorkerCoreUniqueAssignmentId}: Executing task...");
                                                    var executeTaskRequest = new ExecuteTaskRequest
                                                    {
                                                        Descriptor_ = taskDescriptor,
                                                    };
                                                    if (task.GraphTaskSpec.Tool.AutoRecover != null)
                                                    {
                                                        executeTaskRequest.AutoRecover.AddRange(task.GraphTaskSpec.Tool.AutoRecover);
                                                    }
                                                    // @note: This hides MSVC's useless output where it shows you the filename
                                                    // of the file you are compiling.
                                                    executeTaskRequest.IgnoreLines.Add(task.GraphTaskSpec.Task.Caption);
                                                    await core.Request.RequestStream.WriteAsync(new ExecutionRequest
                                                    {
                                                        ExecuteTask = executeTaskRequest
                                                    }, instance.CancellationToken);

                                                    // Stream the results until we get an exit code.
                                                    await using var enumerable = core.Request.GetAsyncEnumerator(instance.CancellationToken);
                                                    ExecuteTaskResponse? finalExecuteTaskResponse = null;
                                                    while (!didComplete && await enumerable.MoveNextAsync(instance.CancellationToken))
                                                    {
                                                        var current = enumerable.Current;
                                                        if (current.ResponseCase != ExecutionResponse.ResponseOneofCase.ExecuteTask)
                                                        {
                                                            throw new RpcException(new Status(
                                                                StatusCode.InvalidArgument,
                                                                "Unexpected task execution response from worker RPC."));
                                                        }
                                                        switch (current.ExecuteTask.Response.DataCase)
                                                        {
                                                            case ProcessResponse.DataOneofCase.StandardOutputLine:
                                                                await responseStream.WriteAsync(new JobResponse
                                                                {
                                                                    TaskOutput = new TaskOutputResponse
                                                                    {
                                                                        Id = task.GraphTaskSpec.Task.Name,
                                                                        StandardOutputLine = current.ExecuteTask.Response.StandardOutputLine,
                                                                    }
                                                                });
                                                                break;
                                                            case ProcessResponse.DataOneofCase.StandardErrorLine:
                                                                await responseStream.WriteAsync(new JobResponse
                                                                {
                                                                    TaskOutput = new TaskOutputResponse
                                                                    {
                                                                        Id = task.GraphTaskSpec.Task.Name,
                                                                        StandardErrorLine = current.ExecuteTask.Response.StandardErrorLine,
                                                                    }
                                                                });
                                                                break;
                                                            case ProcessResponse.DataOneofCase.ExitCode:
                                                                exitCode = current.ExecuteTask.Response.ExitCode;
                                                                finalExecuteTaskResponse = current.ExecuteTask;
                                                                status = exitCode == 0
                                                                    ? TaskCompletionStatus.TaskCompletionSuccess
                                                                    : TaskCompletionStatus.TaskCompletionFailure;
                                                                didComplete = true;
                                                                break;
                                                        }
                                                    }

                                                    if (finalExecuteTaskResponse == null)
                                                    {
                                                        // This should never be null since we break the loop on ExitCode.
                                                        throw new InvalidOperationException();
                                                    }

                                                    // Attach any metadata from the task execution.
                                                    // @note: We don't have any metadata for task execution yet.

                                                    // If we were successful, synchronise the output blobs back.
                                                    if (taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote &&
                                                        status == TaskCompletionStatus.TaskCompletionSuccess &&
                                                        finalExecuteTaskResponse.OutputAbsolutePathsToBlobXxHash64 != null &&
                                                        !taskDescriptor.Remote.UseFastLocalExecution)
                                                    {
                                                        // Notify the client we're changing phases.
                                                        await SendPhaseChangeAsync(
                                                            TaskPhase.RemoteOutputBlobSynchronisation,
                                                            finalPhaseMetadata);
                                                        finalPhaseMetadata.Clear();

                                                        var outputBlobSynchronisation = await _blobSynchroniser.SynchroniseOutputBlobsAsync(
                                                            core,
                                                            taskDescriptor.Remote,
                                                            finalExecuteTaskResponse,
                                                            instance.CancellationToken);
                                                        finalPhaseMetadata = new Dictionary<string, string>
                                                        {
                                                            {
                                                                "outputBlobSync.elapsedUtcTicksHashingInputFiles",
                                                                outputBlobSynchronisation.ElapsedUtcTicksHashingInputFiles.ToString(CultureInfo.InvariantCulture)
                                                            },
                                                            {
                                                                "outputBlobSync.elapsedUtcTicksQueryingMissingBlobs",
                                                                outputBlobSynchronisation.ElapsedUtcTicksQueryingMissingBlobs.ToString(CultureInfo.InvariantCulture)
                                                            },
                                                            {
                                                                "outputBlobSync.elapsedUtcTicksTransferringCompressedBlobs",
                                                                outputBlobSynchronisation.ElapsedUtcTicksTransferringCompressedBlobs.ToString(CultureInfo.InvariantCulture)
                                                            },
                                                            {
                                                                "outputBlobSync.compressedDataTransferLength",
                                                                outputBlobSynchronisation.CompressedDataTransferLength.ToString(CultureInfo.InvariantCulture)
                                                            },
                                                        };
                                                    }
                                                }

                                                break;
                                            }
                                        default:
                                            throw new NotSupportedException($"Unknown graph task to execute: {task.GetType().FullName}");
                                    }
                                }
                                finally
                                {
                                    if (localCoreRequest != null)
                                    {
                                        await localCoreRequest.DisposeAsync();
                                    }
                                }
                            }
                            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
                            {
                                // We're stopping because the caller cancelled the build (i.e. the client
                                // of the dispatcher RPC hit "Ctrl-C").
                                status = TaskCompletionStatus.TaskCompletionCancelled;
                            }
                            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && instance.CancellationToken.IsCancellationRequested)
                            {
                                // We're stopping because something else cancelled the build.
                                status = TaskCompletionStatus.TaskCompletionCancelled;
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                // We're stopping because the caller cancelled the build (i.e. the client
                                // of the dispatcher RPC hit "Ctrl-C").
                                status = TaskCompletionStatus.TaskCompletionCancelled;
                            }
                            catch (OperationCanceledException) when (instance.CancellationToken.IsCancellationRequested)
                            {
                                // We're stopping because something else cancelled the build.
                                status = TaskCompletionStatus.TaskCompletionCancelled;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogCritical(ex, $"Exception during task execution: {ex.Message}");
                                status = TaskCompletionStatus.TaskCompletionException;
                                skipEmitComplete = false;
                                exceptionMessage = ex.ToString();
                            }
                        }
                        finally
                        {
                            try
                            {
                                if (!skipEmitComplete)
                                {
                                    if (!didStart)
                                    {
                                        // We never actually started this task because we failed
                                        // to reserve, but we need to start it so we can then immediately
                                        // convey the exception we ran into.
                                        await responseStream.WriteAsync(new JobResponse
                                        {
                                            TaskStarted = new TaskStartedResponse
                                            {
                                                Id = task.GraphTaskSpec.Task.Name,
                                                DisplayName = task.GraphTaskSpec.Task.Caption,
                                                WorkerMachineName = string.Empty,
                                                WorkerCoreNumber = 0,
                                            },
                                        }, instance.CancellationToken);
                                    }
                                    await responseStream.WriteAsync(new JobResponse
                                    {
                                        TaskCompleted = new TaskCompletedResponse
                                        {
                                            Id = task.GraphTaskSpec.Task.Name,
                                            DisplayName = task.GraphTaskSpec.Task.Caption,
                                            Status = status,
                                            ExitCode = exitCode,
                                            ExceptionMessage = exceptionMessage,
                                            TotalSeconds = taskStopwatch.Elapsed.TotalSeconds,
                                            FinalPhaseEndTimeUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
                                            TotalSecondsInPreviousPhase = currentPhaseStopwatch.Elapsed.TotalSeconds,
                                            PreviousPhaseMetadata = { finalPhaseMetadata },
                                        }
                                    }, instance.CancellationToken);
                                }

                                await FinishTaskAsync(graph, instance, task, status, true);
                            }
                            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
                            {
                                // We're stopping because the caller cancelled the build (i.e. the client
                                // of the dispatcher RPC hit "Ctrl-C").
                                instance.CancelDueToException(ex);
                            }
                            catch (OperationCanceledException) when (instance.CancellationToken.IsCancellationRequested)
                            {
                                // We're stopping because something else cancelled the build.
                            }
                            catch (ObjectDisposedException ex) when (ex.Message.Contains("Request has finished and HttpContext disposed."))
                            {
                                // We can't send further messages because the response stream has died.
                            }
                            catch (Exception ex)
                            {
                                // If any of this fails, we have to cancel the build.
                                _logger.LogCritical(ex, $"Exception during task execution finalisation: {ex.Message}");
                                instance.CancelDueToException(ex);
                            }
                        }

                        static async Task FinishTaskAsync(
                            Graph graph,
                            GraphExecutionInstance instance,
                            GraphTask task,
                            TaskCompletionStatus status,
                            bool scheduleDownstream)
                        {
                            if (status == TaskCompletionStatus.TaskCompletionSuccess)
                            {
                                // This task succeeded, queue up downstream tasks for scheduling.
                                await instance.CompletedAndScheduledTasksLock.WaitAsync();
                                try
                                {
                                    instance.CompletedTasks.Add(task);
                                    if (Interlocked.Decrement(ref instance.RemainingTasks) != 0)
                                    {
                                        if (scheduleDownstream)
                                        {
                                            // What is waiting on this task?
                                            var dependsOn = graph.TaskDependencies.WhatDependsOnTarget(task);
                                            foreach (var depend in dependsOn)
                                            {
                                                // Is this task already scheduled or completed?
                                                if (instance.CompletedTasks.Contains(depend) ||
                                                    instance.ScheduledTasks.Contains(depend))
                                                {
                                                    continue;
                                                }

                                                // Are all the dependencies of this waiting task now satisified?
                                                var waitingOn = graph.TaskDependencies.WhatTargetDependsOn(depend);
                                                if (instance.CompletedTasks.IsSupersetOf(waitingOn))
                                                {
                                                    // This task is now ready to schedule.
                                                    instance.QueuedTasksForScheduling.Enqueue(depend);
                                                    instance.QueuedTaskAvailableForScheduling.Release();
                                                    instance.ScheduledTasks.Add(depend);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Make sure our scheduling loop gets a chance to check
                                        // RemainingTasks.
                                        instance.QueuedTaskAvailableForScheduling.Release();
                                    }
                                }
                                finally
                                {
                                    instance.CompletedAndScheduledTasksLock.Release();
                                }
                            }
                            else
                            {
                                // This task failed, we won't run downstream tasks.
                                // What is waiting on this task?
                                var dependsOn = new HashSet<GraphTask>();
                                graph.TaskDependencies.WhatDependsOnTargetRecursive(task, dependsOn);
                                foreach (var depend in dependsOn)
                                {
                                    // Is this task still pending? If so, we won't be running it.
                                    if (!instance.CancelledDueToUpstreamFailureTasks.Contains(depend))
                                    {
                                        Interlocked.Decrement(ref instance.RemainingTasks);
                                        instance.CancelledDueToUpstreamFailureTasks.Add(depend);
                                        continue;
                                    }
                                }

                                // Make sure our scheduling loop gets a chance to check
                                // RemainingTasks.
                                instance.QueuedTaskAvailableForScheduling.Release();
                            }
                        }
                    }));
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (instance.RemainingTasks == 0 &&
                    instance.CompletedTasks.Count == graph.Tasks.Count)
                {
                    // All tasks completed successfully.
                    await responseStream.WriteAsync(new JobResponse
                    {
                        JobComplete = new JobCompleteResponse
                        {
                            Status = JobCompletionStatus.JobCompletionSuccess,
                            TotalSeconds = graphStopwatch.Elapsed.TotalSeconds,
                        }
                    });
                }
                else
                {
                    // Something failed.
                    await responseStream.WriteAsync(new JobResponse
                    {
                        JobComplete = new JobCompleteResponse
                        {
                            Status = JobCompletionStatus.JobCompletionFailure,
                            TotalSeconds = graphStopwatch.Elapsed.TotalSeconds,
                        }
                    });
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
            {
                // Convert the RPC exception into an OperationCanceledException.
                throw new OperationCanceledException("Cancellation via RPC", ex);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // We're stopping because the caller cancelled the build (i.e. the client
                // of the dispatcher RPC hit "Ctrl-C").
                throw;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (instance.IsCancelledDueToException)
                {
                    // This is a propagation of build cancellation due to
                    // task failure, which can happen due to the top-level
                    // instance.QueuedTaskAvailableForScheduling.WaitAsync call.
                    // In this case, we want to wait until all the scheduled
                    // executions complete so task-level failures propagate to
                    // the stream before we issue a JobCompleteResponse.
                    foreach (var execution in instance.ScheduledExecutions)
                    {
                        try
                        {
                            await execution;
                        }
                        catch
                        {
                        }
                    }
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        await responseStream.WriteAsync(new JobResponse
                        {
                            JobComplete = new JobCompleteResponse
                            {
                                Status = JobCompletionStatus.JobCompletionFailure,
                                TotalSeconds = graphStopwatch.Elapsed.TotalSeconds,
                                ExceptionMessage = instance.ExceptionMessage ?? string.Empty,
                            }
                        });
                    }
                }
                else
                {
                    throw;
                }
            }
        }
    }
}