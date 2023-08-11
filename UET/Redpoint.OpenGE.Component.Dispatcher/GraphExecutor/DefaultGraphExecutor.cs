namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Google.Protobuf.Collections;
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.Remoting;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using System.Collections.Concurrent;
    using System.Diagnostics;
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
            public required IWorkerPool WorkerPool;
            public long RemainingTasks;
            public readonly SemaphoreSlim QueuedTaskAvailableForScheduling = new SemaphoreSlim(0);
            public readonly ConcurrentQueue<GraphTask> QueuedTasksForScheduling = new ConcurrentQueue<GraphTask>();
            private CancellationTokenSource _cancellationTokenSource;
            public CancellationToken CancellationToken => _cancellationTokenSource.Token;
            public readonly List<Task> ScheduledExecutions = new List<Task>();
            public readonly SemaphoreSlim ScheduledExecutionsLock = new SemaphoreSlim(1);
            public readonly HashSet<GraphTask> ScheduledTasks = new HashSet<GraphTask>();
            public readonly HashSet<GraphTask> CompletedTasks = new HashSet<GraphTask>();
            public readonly SemaphoreSlim CompletedAndScheduledTasksLock = new SemaphoreSlim(1);
            public bool IsCancelledDueToFailure { get; private set; }

            public GraphExecutionInstance(CancellationToken cancellationToken)
            {
                _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
            }

            internal void CancelDueToFailure()
            {
                IsCancelledDueToFailure = true;
                _cancellationTokenSource.Cancel();
            }

            internal void CancelDueToException(Exception ex)
            {
                IsCancelledDueToFailure = true;
                _cancellationTokenSource.Cancel();
            }
        }

        public async Task ExecuteGraphAsync(
            IWorkerPool workerPool,
            Graph graph,
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
                        throw new RpcException(new Status(
                            StatusCode.Internal,
                            "Queued task semaphore indicated a task could be scheduled, but nothing was available in the queue."));
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
                        try
                        {
                            try
                            {
                                // Do descriptor generation based on the task.
                                TaskDescriptor taskDescriptor;
                                switch (task)
                                {
                                    case DescribingGraphTask describingGraphTask:
                                        // Generate the task descriptor from the factory. This can take a while
                                        // if we're parsing preprocessor headers.
                                        Stopwatch? prepareStopwatch = null;
                                        if (!string.IsNullOrWhiteSpace(describingGraphTask.TaskDescriptorFactory.PreparationOperationDescription))
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
                                            instance.CancellationToken);
                                        if (prepareStopwatch != null)
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
                                        break;
                                    case FastExecutableGraphTask fastExecutableGraphTask:
                                        taskDescriptor = await fastExecutableGraphTask.TaskDescriptorFactory.CreateDescriptorForTaskSpecAsync(
                                            fastExecutableGraphTask.GraphTaskSpec,
                                            instance.CancellationToken);
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
                                    case DescribingGraphTask:
                                        // No execution work to do for this task.
                                        exitCode = 0;
                                        status = TaskCompletionStatus.TaskCompletionSuccess;
                                        didComplete = true;
                                        skipEmitComplete = true;
                                        break;
                                    default:
                                        {
                                            // Reserve a core from somewhere...
                                            await using var core = await instance.WorkerPool.ReserveCoreAsync(
                                                taskDescriptor.DescriptorCase != TaskDescriptor.DescriptorOneofCase.Remote,
                                                instance.CancellationToken);

                                            // We're now going to start doing the work for this task.
                                            taskStopwatch.Start();
                                            await responseStream.WriteAsync(new JobResponse
                                            {
                                                TaskStarted = new TaskStartedResponse
                                                {
                                                    Id = task.GraphTaskSpec.Task.Name,
                                                    DisplayName = task.GraphTaskSpec.Task.Caption,
                                                    WorkerMachineName = core.WorkerMachineName,
                                                    WorkerCoreNumber = core.WorkerCoreNumber,
                                                },
                                            }, instance.CancellationToken);
                                            didStart = true;

                                            // Perform synchronisation for remote tasks.
                                            if (taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote)
                                            {
                                                // Synchronise the tool and determine the hash to
                                                // use for the actual request.
                                                var toolExecutionInfo = await _toolSynchroniser.SynchroniseToolAndGetXxHash64(
                                                    core,
                                                    taskDescriptor.Remote.ToolLocalAbsolutePath,
                                                    instance.CancellationToken);
                                                taskDescriptor.Remote.ToolExecutionInfo = toolExecutionInfo;

                                                // Synchronise all of the input blobs.
                                                var inputsByBlobXxHash64 = await _blobSynchroniser.SynchroniseInputBlobs(
                                                    core,
                                                    taskDescriptor.Remote,
                                                    instance.CancellationToken);
                                                taskDescriptor.Remote.InputsByBlobXxHash64 = inputsByBlobXxHash64;
                                            }

                                            // Execute the task on the core.
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

                                            // If we were successful, synchronise the output blobs back.
                                            if (taskDescriptor.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Remote && 
                                                status == TaskCompletionStatus.TaskCompletionSuccess)
                                            {
                                                if (finalExecuteTaskResponse == null)
                                                {
                                                    // This should never be null since we break the loop on ExitCode.
                                                    throw new InvalidOperationException();
                                                }

                                                await _blobSynchroniser.SynchroniseOutputBlobs(
                                                    core,
                                                    taskDescriptor.Remote,
                                                    finalExecuteTaskResponse,
                                                    instance.CancellationToken);
                                            }

                                            break;
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
                                        }
                                    }, instance.CancellationToken);
                                }

                                if (status == TaskCompletionStatus.TaskCompletionSuccess)
                                {
                                    // This task succeeded, queue up downstream tasks for scheduling.
                                    await instance.CompletedAndScheduledTasksLock.WaitAsync();
                                    try
                                    {
                                        instance.CompletedTasks.Add(task);
                                        if (Interlocked.Decrement(ref instance.RemainingTasks) != 0)
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
                                    // This task failed, cancel the build.
                                    instance.CancelDueToFailure();
                                }
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
                if (instance.IsCancelledDueToFailure)
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
