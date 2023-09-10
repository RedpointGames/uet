namespace Redpoint.Tasks
{
    using Redpoint.Concurrency;

    internal class DefaultTaskSchedulerScope : ITaskSchedulerScope
    {
        private readonly string _scopeName;
        private readonly CancellationTokenSource _scopeCancellationTokenSource;
        private readonly List<ScheduledTask> _tasks;
        private readonly MutexSlim _tasksMutex;

        private class ScheduledTask
        {
            public required string Name;
            public required CancellationTokenSource CancellationTokenSource;
            public Task? InternalTask;
        }

        public DefaultTaskSchedulerScope(
            string scopeName,
            CancellationToken scopeCancellationToken)
        {
            _scopeName = scopeName;
            _scopeCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(scopeCancellationToken);
            _tasks = new List<ScheduledTask>();
            _tasksMutex = new MutexSlim();
        }

        public string[] GetCurrentlyExecutingTasks()
        {
            using (_tasksMutex.Wait())
            {
                return _tasks.Select(x => $"{_scopeName}:{x.Name}").ToArray();
            }
        }

        public async Task RunAsync(string taskName, CancellationToken taskCancellationToken, Func<CancellationToken, Task> backgroundTask)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(
                _scopeCancellationTokenSource.Token,
                taskCancellationToken);
            var scheduledTask = new ScheduledTask
            {
                Name = taskName,
                CancellationTokenSource = cts,
            };
            using (await _tasksMutex.WaitAsync(CancellationToken.None))
            {
                _tasks.Add(scheduledTask);
            }
            scheduledTask.InternalTask = Task.Run(async () =>
            {
                try
                {
                    await backgroundTask(cts.Token);
                }
                finally
                {
                    using (await _tasksMutex.WaitAsync(CancellationToken.None))
                    {
                        _tasks.Remove(scheduledTask);
                    }
                }
            });
            await scheduledTask.InternalTask;
        }

        public async ValueTask DisposeAsync()
        {
            var exceptions = new List<Exception>();
            _scopeCancellationTokenSource.Cancel();
            List<ScheduledTask> tasksCopy;
            using (await _tasksMutex.WaitAsync(CancellationToken.None))
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
                        await internalTask;
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

        public void Dispose()
        {
            _scopeCancellationTokenSource.Cancel();
            // @note: We can't await on the remaining tasks for this type of dispose.
        }
    }
}