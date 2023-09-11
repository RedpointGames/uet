namespace Redpoint.Tasks
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using System.Diagnostics;

    internal sealed class DefaultTaskSchedulerScope : ITaskSchedulerScope
    {
        private readonly ILogger _logger;
        private readonly string _scopeName;
        private readonly CancellationTokenSource _scopeCancellationTokenSource;
        private readonly List<ScheduledTask> _tasks;
        private readonly Mutex _tasksMutex;

        private sealed class ScheduledTask
        {
            public required string Name;
            public required CancellationTokenSource CancellationTokenSource;
            public Task? InternalTask;
        }

        public DefaultTaskSchedulerScope(
            ILogger logger,
            string scopeName,
            CancellationToken scopeCancellationToken)
        {
            _logger = logger;
            _scopeName = scopeName;
            _scopeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(scopeCancellationToken);
            _tasks = new List<ScheduledTask>();
            _tasksMutex = new Mutex();
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"A new task scheduling scope has been created: {_scopeName}");
            }
        }

        public string[] GetCurrentlyExecutingTasks()
        {
            using (_tasksMutex.Wait(CancellationToken.None))
            {
                return _tasks.Select(x => $"{_scopeName}:{x.Name}").ToArray();
            }
        }

        public async Task RunAsync(string taskName, Func<CancellationToken, Task> backgroundTask, CancellationToken taskCancellationToken)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                _scopeCancellationTokenSource.Token,
                taskCancellationToken);
            var scheduledTask = new ScheduledTask
            {
                Name = taskName,
                CancellationTokenSource = cts,
            };
            using (await _tasksMutex.WaitAsync(cts.Token).ConfigureAwait(false))
            {
                _tasks.Add(scheduledTask);
            }
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace($"Scheduling task: {_scopeName}/{taskName}");
            }
            scheduledTask.InternalTask = Task.Run(async () =>
            {
                try
                {
                    await backgroundTask(cts.Token).ConfigureAwait(false);
                }
                finally
                {
                    if (_logger.IsEnabled(LogLevel.Trace))
                    {
                        _logger.LogTrace($"Scheduled task ended: {_scopeName}/{taskName}");
                    }
                    using (await _tasksMutex.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                    {
                        _tasks.Remove(scheduledTask);
                    }
                    cts.Dispose();
                }
            }, cts.Token);
            await scheduledTask.InternalTask.ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            var st = _logger.IsEnabled(LogLevel.Trace) ? Stopwatch.StartNew() : null;
            try
            {
                var exceptions = new List<Exception>();
                _scopeCancellationTokenSource.Cancel();
                List<ScheduledTask> tasksCopy;
                using (await _tasksMutex.WaitAsync(CancellationToken.None).ConfigureAwait(false))
                {
                    tasksCopy = _tasks.ToList();
                    _tasks.Clear();
                }
                foreach (var task in tasksCopy)
                {
                    try
                    {
                        var internalTask = task.InternalTask;
                        if (internalTask != null)
                        {
                            await internalTask.ConfigureAwait(false);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Consume this exception.
                    }
                    catch (Exception ex)
                    {
                        // Track this exception so we can propagate it.
                        exceptions.Add(ex);
                    }
                }
                if (exceptions.Count > 0)
                {
                    throw new AggregateException(exceptions);
                }
            }
            finally
            {
                if (_logger.IsEnabled(LogLevel.Trace) && st != null)
                {
                    _logger.LogTrace($"Took {st.ElapsedMilliseconds} milliseconds to shutdown scheduling scope: {_scopeName}");
                }
                _scopeCancellationTokenSource.Dispose();
            }
        }

        public void Dispose()
        {
            _scopeCancellationTokenSource.Cancel();
            _scopeCancellationTokenSource.Dispose();
            // @note: We can't await on the remaining tasks for this type of dispose.
        }
    }
}