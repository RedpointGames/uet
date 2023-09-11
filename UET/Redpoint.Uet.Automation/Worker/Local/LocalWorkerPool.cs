namespace Redpoint.Uet.Automation.Worker.Local
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.ProcessExecution;
    using Redpoint.Reservation;
    using Redpoint.Uet.Automation.SystemResources;
    using Redpoint.Uet.Automation.TestLogging;
    using Redpoint.Uet.Uat;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class LocalWorkerPool : IWorkerPool
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<LocalWorkerPool> _logger;
        private readonly ITestLogger _testLogger;
        private readonly ISystemResources _systemResources;
        private readonly DesiredWorkerDescriptor[] _workerDescriptors;
        private readonly OnWorkerStarted _onWorkerStarted;
        private readonly OnWorkerExited _onWorkedExited;
        private readonly OnWorkerPoolFailure _onWorkerPoolFailure;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private List<LocalWorker> _currentWorkers;
        private List<LocalWorker> _reservedWorkers;
        private readonly HashSet<DesiredWorkerDescriptor> _descriptorsWindingDown;
        private readonly ILoopbackPortReservationManager _loopbackPortReservationManager;
        private Task _runLoopTask;
        private bool _disposed;

        public LocalWorkerPool(
            IServiceProvider serviceProvider,
            ILogger<LocalWorkerPool> logger,
            ITestLogger testLogger,
            IEnumerable<DesiredWorkerDescriptor> workerDescriptors,
            ISystemResources systemResources,
            IReservationManagerFactory reservationManagerFactory,
            OnWorkerStarted onWorkerStarted,
            OnWorkerExited onWorkedExited,
            OnWorkerPoolFailure onWorkerPoolFailure,
            CancellationToken cancellationToken)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _testLogger = testLogger;
            _systemResources = systemResources;
            _workerDescriptors = workerDescriptors.ToArray();
            _onWorkerStarted = onWorkerStarted;
            _onWorkedExited = onWorkedExited;
            _onWorkerPoolFailure = onWorkerPoolFailure;
            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _currentWorkers = new List<LocalWorker>();
            _reservedWorkers = new List<LocalWorker>();
            _descriptorsWindingDown = new HashSet<DesiredWorkerDescriptor>();
            _loopbackPortReservationManager = reservationManagerFactory.CreateLoopbackPortReservationManager();
            _runLoopTask = Task.Run(RunLoopAsync, cancellationToken);
        }

        public Task<IAsyncDisposable> ReserveAsync(IWorker worker)
        {
            if (!_currentWorkers.Contains(worker))
            {
                throw new InvalidOperationException("This worker is not from this worker pool or has since exited.");
            }
            if (_reservedWorkers.Contains(worker))
            {
                throw new InvalidOperationException("This worker is already reserved.");
            }
            _reservedWorkers.Add((LocalWorker)worker);
            return Task.FromResult<IAsyncDisposable>(new WorkerReservation(this, (LocalWorker)worker));
        }

        private sealed class WorkerReservation : IAsyncDisposable
        {
            private readonly LocalWorkerPool _workerPool;
            private readonly LocalWorker _worker;

            public WorkerReservation(LocalWorkerPool workerPool, LocalWorker worker)
            {
                _workerPool = workerPool;
                _worker = worker;
            }

            public ValueTask DisposeAsync()
            {
                _workerPool._reservedWorkers.Remove(_worker);
                return ValueTask.CompletedTask;
            }
        }

        private async Task OnInternalWorkerStarted(IWorker worker)
        {
            await _onWorkerStarted(worker).ConfigureAwait(false);
        }

        private async Task OnInternalWorkerExited(IWorker worker, int exitCode, IWorkerCrashData? crashData)
        {
            _currentWorkers.Remove((LocalWorker)worker);
            _reservedWorkers.Remove((LocalWorker)worker);
            await _testLogger.LogWorkerStopped(worker, crashData).ConfigureAwait(false);
            await _onWorkedExited(worker, exitCode, crashData).ConfigureAwait(false);
        }

        private async Task RunLoopAsync()
        {
            var hostPlatform = OperatingSystem.IsWindows() ? "Win64" : "Mac";

            try
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    // Check to see if we're meeting our desired workers.
                    foreach (var workerDescriptor in _workerDescriptors)
                    {
                        var currentWorkers = _currentWorkers.Where(x => x.Descriptor == workerDescriptor).ToArray();

                        // Compute the target worker count.
                        var targetWorkerCount = workerDescriptor.MinWorkerCount ?? 1;
                        if (workerDescriptor.Platform == hostPlatform &&
                            _systemResources.CanQuerySystemResources)
                        {
                            var (availableMemory, totalMemory) = await _systemResources.GetMemoryInfo().ConfigureAwait(false);
                            var unrealRequiredMemory = (16uL * 1024 * 1024 * 1024);
                            var consumedMemory = (uint)targetWorkerCount * unrealRequiredMemory;
                            var memoryBasedWorkerCount = targetWorkerCount;
                            while (consumedMemory + unrealRequiredMemory < availableMemory &&
                                (!workerDescriptor.MaxWorkerCount.HasValue || memoryBasedWorkerCount < workerDescriptor.MaxWorkerCount.Value))
                            {
                                memoryBasedWorkerCount += 1;
                                consumedMemory += unrealRequiredMemory;
                            }
                            targetWorkerCount = memoryBasedWorkerCount;
                        }

                        if (currentWorkers.Length < targetWorkerCount && !_descriptorsWindingDown.Contains(workerDescriptor))
                        {
                            _logger.LogTrace($"There are currently {currentWorkers.Length} workers and the target is {targetWorkerCount}. Starting a new worker...");

                            var newId = Guid.NewGuid().ToString();
                            var displayName = Enumerable.Range(1, targetWorkerCount + 1)
                                .Select(x => $"{workerDescriptor.Platform}.{x}")
                                .First(x => !currentWorkers.Any(y => y.DisplayName == x));

                            var portReservation = await _loopbackPortReservationManager.ReserveAsync().ConfigureAwait(false);
                            var didConsumePort = false;
                            try
                            {
                                LocalWorker newWorker;
                                if (workerDescriptor.IsEditor)
                                {
                                    if (!workerDescriptor.Platform.Equals(hostPlatform, StringComparison.OrdinalIgnoreCase))
                                    {
                                        await _onWorkerPoolFailure("A worker descriptor was marked as IsEditor but the target platform does not match the host platform.").ConfigureAwait(false);
                                        _cancellationTokenSource.Cancel();
                                        _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                                    }

                                    newWorker = new LocalEditorWorker(
                                        _serviceProvider.GetRequiredService<ILogger<LocalEditorWorker>>(),
                                        _serviceProvider.GetRequiredService<IProcessExecutor>(),
                                        newId,
                                        displayName,
                                        portReservation,
                                        workerDescriptor,
                                        OnInternalWorkerStarted,
                                        OnInternalWorkerExited);
                                    await _testLogger.LogWorkerStarting(newWorker).ConfigureAwait(false);
                                }
                                else
                                {
                                    newWorker = new LocalGauntletWorker(
                                        _serviceProvider.GetRequiredService<ILogger<LocalGauntletWorker>>(),
                                        _serviceProvider.GetRequiredService<IProcessExecutor>(),
                                        _serviceProvider.GetRequiredService<IUATExecutor>(),
                                        newId,
                                        displayName,
                                        portReservation,
                                        workerDescriptor,
                                        OnInternalWorkerStarted,
                                        OnInternalWorkerExited);
                                    await _testLogger.LogWorkerStarting(newWorker).ConfigureAwait(false);
                                }
                                _currentWorkers.Add(newWorker);
                                didConsumePort = true;
                                newWorker.StartInBackground();
                            }
                            finally
                            {
                                if (!didConsumePort)
                                {
                                    await portReservation.DisposeAsync().ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    // Wait a little bit before checking again.
                    await Task.Delay(1000, _cancellationTokenSource.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await _onWorkerPoolFailure($"Unexpected exception in worker pool background loop: {ex}").ConfigureAwait(false);
            }
            finally
            {
                _reservedWorkers.Clear();
                foreach (var worker in _currentWorkers.ToArray())
                {
                    _logger.LogTrace($"Waiting for worker {worker.Id} to stop...");
                    await worker.DisposeAsync().ConfigureAwait(false);
                }
                _currentWorkers.Clear();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _cancellationTokenSource.Cancel();
                try
                {
                    await _runLoopTask.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    // This is expected.
                }
                _cancellationTokenSource.Dispose();
            }
        }

        public void FinishedWithWorker(IWorker worker)
        {
            if (_currentWorkers.Contains(worker))
            {
                _logger.LogTrace($"Caller indicated that worker {worker.Id} is no longer needed. Stopping the worker and marking the descriptor as winding down...");

                _descriptorsWindingDown.Add(worker.Descriptor);
                if (_reservedWorkers.Contains(worker))
                {
                    _reservedWorkers.Remove((LocalWorker)worker);
                }
                // @note: We don't wait for this to complete, because we just need to get the cancellation token cancelled.
                // If this function was async and awaited this task, it would induce a deadlock if FinishedWithWorker was
                // called from OnWorkerStarted.
#pragma warning disable CA2012
                _ = ((LocalWorker)worker).DisposeAsync();
#pragma warning restore CA2012
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        public void FinishedWithDescriptor(DesiredWorkerDescriptor descriptor)
        {
            _logger.LogTrace($"Caller indicated that descriptor {descriptor.Platform} is no longer needed. Stopping the worker and marking the descriptor as winding down...");

            _descriptorsWindingDown.Add(descriptor);
            foreach (var worker in _currentWorkers.ToArray())
            {
                if (worker.Descriptor == descriptor)
                {
                    if (_reservedWorkers.Contains(worker))
                    {
                        _reservedWorkers.Remove(worker);
                    }
                    // @note: We don't wait for this to complete, because we just need to get the cancellation token cancelled.
                    // If this function was async and awaited this task, it would induce a deadlock if FinishedWithWorker was
                    // called from OnWorkerStarted.
#pragma warning disable CA2012
                    _ = worker.DisposeAsync();
#pragma warning restore CA2012
                }
            }
        }

        public void KillWorker(IWorker worker)
        {
            if (_currentWorkers.Contains(worker))
            {
                _logger.LogTrace($"Caller indicated that worker {worker.Id} should be killed. Stopping the worker without winding down the descriptor...");

                if (_reservedWorkers.Contains(worker))
                {
                    _reservedWorkers.Remove((LocalWorker)worker);
                }
                // @note: We don't wait for this to complete, because we just need to get the cancellation token cancelled.
                // If this function was async and awaited this task, it would induce a deadlock if KillWorker was
                // called from OnWorkerStarted.
#pragma warning disable CA2012
                _ = ((LocalWorker)worker).DisposeAsync();
#pragma warning restore CA2012
            }
            else
            {
                throw new InvalidOperationException();
            }
        }
    }
}
