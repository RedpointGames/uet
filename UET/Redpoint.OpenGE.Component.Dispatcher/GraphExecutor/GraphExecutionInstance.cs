namespace Redpoint.OpenGE.Component.Dispatcher.GraphExecutor
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.StallDiagnostics;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.Protocol;
    using Redpoint.Tasks;
    using System.Collections.Concurrent;
    using System.Threading.Tasks;

    internal class GraphExecutionInstance
    {
        private readonly Graph _graph;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Dictionary<GraphTask, GraphTaskStatus> _taskStatuses;
        private readonly Mutex _taskStatusesLock = new Mutex();

        public required ITaskApiWorkerPool WorkerPool;
        public readonly TerminableAwaitableConcurrentQueue<GraphTask> QueuedTasksForScheduling = new TerminableAwaitableConcurrentQueue<GraphTask>();
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;
        public readonly List<Task> ScheduledExecutions = new List<Task>();
        public bool IsCancelledDueToException { get; private set; }
        public string? ExceptionMessage { get; private set; }
        public IStallMonitor? StallMonitor { get; set; }

        public GraphExecutionInstance(
            Graph graph,
            CancellationToken cancellationToken)
        {
            _graph = graph;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            _taskStatuses = graph.Tasks.ToDictionary(
                k => k.Value,
                v => GraphTaskStatus.Pending);
        }

        internal async ValueTask<IReadOnlyDictionary<GraphTask, GraphTaskStatus>> GetTaskStatusesAsync()
        {
            using (await _taskStatusesLock.WaitAsync(CancellationToken.None).ConfigureAwait(false))
            {
                return _taskStatuses.ToDictionary(k => k.Key, v => v.Value);
            }
        }

        internal async ValueTask<bool> DidAllTasksCompleteSuccessfullyAsync()
        {
            using (await _taskStatusesLock.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                foreach (var kv in _taskStatuses)
                {
                    if (kv.Value != GraphTaskStatus.CompletedSuccessfully)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        internal async ValueTask<bool> AreAnyTasksScheduledAsync()
        {
            using (await _taskStatusesLock.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                return _taskStatuses.Any(kv => kv.Value == GraphTaskStatus.Scheduled);
            }
        }

        internal async ValueTask ScheduleInitialTasksAsync()
        {
            using (await _taskStatusesLock.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                // Schedule up all of the tasks that can be immediately
                // scheduled.
                foreach (var taskKv in _graph.Tasks)
                {
                    if (_graph.TaskDependencies.WhatTargetDependsOn(taskKv.Value).Count == 0)
                    {
                        _taskStatuses[taskKv.Value] = GraphTaskStatus.Scheduled;
                        QueuedTasksForScheduling.Enqueue(taskKv.Value);
                        StallMonitor?.MadeProgress();
                    }
                }
            }
        }

        internal async ValueTask SetTaskStatusAsync(GraphTask task, GraphTaskStatus status)
        {
            if (status > GraphTaskStatus.Scheduled &&
                status < GraphTaskStatus.CompletedSuccessfully)
            {
                StallMonitor?.MadeProgress();
                using (await _taskStatusesLock.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
                {
                    _taskStatuses[task] = status;
                }
            }
        }

        internal async ValueTask FinishTaskAsync(GraphTask task, TaskCompletionStatus status, GraphExecutionDownstreamScheduling schedulingBehaviour)
        {
            StallMonitor?.MadeProgress();
            using (await _taskStatusesLock.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false))
            {
                if (status == TaskCompletionStatus.TaskCompletionSuccess)
                {
                    // This task succeeded, queue up downstream tasks for scheduling.
                    _taskStatuses[task] = GraphTaskStatus.CompletedSuccessfully;

                    if (schedulingBehaviour == GraphExecutionDownstreamScheduling.ScheduleByGraphExecution)
                    {
                        // What is waiting on this task?
                        var dependsOn = _graph.TaskDependencies.WhatDependsOnTarget(task);
                        foreach (var depend in dependsOn)
                        {
                            // Is this task already scheduled or completed?
                            if (_taskStatuses[depend] != GraphTaskStatus.Pending)
                            {
                                continue;
                            }

                            // Are all the dependencies of this waiting task now satisified?
                            var waitingOn = _graph.TaskDependencies.WhatTargetDependsOn(depend);
                            var allWaitingOnCompletedSuccessfully = true;
                            foreach (var waitingOnTask in waitingOn)
                            {
                                if (_taskStatuses[waitingOnTask] != GraphTaskStatus.CompletedSuccessfully)
                                {
                                    allWaitingOnCompletedSuccessfully = false;
                                    break;
                                }
                            }
                            if (allWaitingOnCompletedSuccessfully)
                            {
                                // This task is now ready to schedule.
                                QueuedTasksForScheduling.Enqueue(depend);
                                StallMonitor?.MadeProgress();
                                _taskStatuses[depend] = GraphTaskStatus.Scheduled;
                            }
                        }
                    }
                    else
                    {
                        // The downstream tasks are about to be immediately worked on. Mark them as scheduled.
                        foreach (var downstream in _graph.TaskDependencies.WhatDependsOnTarget(task))
                        {
                            _taskStatuses[downstream] = GraphTaskStatus.Scheduled;
                        }
                    }
                }
                else
                {
                    // This task failed, we won't run downstream tasks.
                    _taskStatuses[task] = GraphTaskStatus.CompletedUnsuccessfully;

                    // What is waiting on this task?
                    var dependsOn = new HashSet<GraphTask>();
                    _graph.TaskDependencies.WhatDependsOnTargetRecursive(task, dependsOn);
                    foreach (var depend in dependsOn)
                    {
                        // Is this task still pending? If so, we won't be running it.
                        if (_taskStatuses[depend] == GraphTaskStatus.Pending)
                        {
                            _taskStatuses[depend] = GraphTaskStatus.CancelledDueToUpstreamFailures;
                            continue;
                        }
                    }
                }

                // If we have no more schedulable tasks, terminate.
                if (!_taskStatuses.Any(kv => kv.Value < GraphTaskStatus.CompletedSuccessfully))
                {
                    QueuedTasksForScheduling.Terminate();
                }
            }
        }

        internal void CancelEntireBuildDueToException(Exception ex)
        {
            IsCancelledDueToException = true;
            ExceptionMessage = ex.ToString();
            _cancellationTokenSource.Cancel();
        }
    }
}
