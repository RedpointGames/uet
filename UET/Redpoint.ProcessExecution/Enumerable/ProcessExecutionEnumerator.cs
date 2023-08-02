namespace Redpoint.ProcessExecution.Enumerable
{
    using Redpoint.ProcessExecution;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    internal class ProcessExecutionEnumerable : IAsyncEnumerable<ProcessResponse>, ICaptureSpecification
    {
        private readonly IProcessExecutor _executor;
        private readonly ProcessSpecification _processSpecification;
        private readonly CancellationTokenSource _processCancellationTokenSource;
        private readonly List<ResponseQueue> _connectedQueues;
        private readonly ReaderWriterLockSlim _connectedQueuesLock;
        private readonly SemaphoreSlim _processStartSemaphore;
        private int _enumeratorCount;
        private Task? _executingProcess;
        private ExitCodeResponse? _exitResponse;

        public ProcessExecutionEnumerable(
            IProcessExecutor executor,
            ProcessSpecification processSpecification,
            CancellationToken processCancellationToken)
        {
            _executor = executor;
            _processSpecification = processSpecification;
            _processCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(processCancellationToken);
            _connectedQueues = new List<ResponseQueue>();
            _connectedQueuesLock = new ReaderWriterLockSlim();
            _processStartSemaphore = new SemaphoreSlim(1);
            _enumeratorCount = 0;
            _executingProcess = null;
            _exitResponse = null;
        }

        private class ResponseQueue
        {
            public SemaphoreSlim _responseAvailable = new SemaphoreSlim(0);
            public ConcurrentQueue<ProcessResponse> _responseQueue = new ConcurrentQueue<ProcessResponse>();
        }

        public IAsyncEnumerator<ProcessResponse> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            _connectedQueuesLock.EnterWriteLock();
            try
            {
                var queue = new ResponseQueue();
                _connectedQueues.Add(queue);
                return new ProcessExecutionEnumerator(
                    this,
                    queue,
                    CancellationTokenSource.CreateLinkedTokenSource(
                        _processCancellationTokenSource.Token,
                        cancellationToken).Token);
            }
            finally
            {
                _connectedQueuesLock.ExitWriteLock();
            }
        }

        private async void EnsureProcessIsRunning()
        {
            if (_executingProcess != null)
            {
                return;
            }

            await _processStartSemaphore.WaitAsync();
            try
            {
                if (_executingProcess != null)
                {
                    return;
                }

                // Start the process.
                _executingProcess = Task.Run(async () =>
                {
                    try
                    {
                        var exitCode = await _executor.ExecuteAsync(
                            _processSpecification,
                            this,
                            _processCancellationTokenSource.Token);
                        SendMessageToEnumerableQueues(new ExitCodeResponse
                        {
                            ExitCode = exitCode,
                        });
                    }
                    catch (OperationCanceledException) when (_processCancellationTokenSource.IsCancellationRequested)
                    {
                        // This is expected.
                        SendMessageToEnumerableQueues(new ExitCodeResponse
                        {
                            ExitCode = 1,
                        });
                    }
                    catch (Exception ex)
                    {
                        SendMessageToEnumerableQueues(new InternalExceptionResponse
                        {
                            Exception = ex,
                        });
                        SendMessageToEnumerableQueues(new ExitCodeResponse
                        {
                            ExitCode = 1,
                        });
                    }
                });
            }
            finally
            {
                _processStartSemaphore.Release();
            }
        }

        private void SendMessageToEnumerableQueues(ProcessResponse message)
        {
            if (message is ExitCodeResponse exitCode)
            {
                _exitResponse = exitCode;
            }
            _connectedQueuesLock.EnterReadLock();
            try
            {
                foreach (var q in _connectedQueues)
                {
                    q._responseQueue.Enqueue(message);
                    q._responseAvailable.Release();
                }
            }
            finally
            {
                _connectedQueuesLock.ExitReadLock();
            }
        }

        bool ICaptureSpecification.InterceptStandardInput => false;

        bool ICaptureSpecification.InterceptStandardOutput => true;

        bool ICaptureSpecification.InterceptStandardError => true;

        void ICaptureSpecification.OnReceiveStandardError(string data)
        {
            SendMessageToEnumerableQueues(new StandardErrorResponse
            {
                Data = data,
            });
        }

        void ICaptureSpecification.OnReceiveStandardOutput(string data)
        {
            SendMessageToEnumerableQueues(new StandardOutputResponse
            {
                Data = data,
            });
        }

        string? ICaptureSpecification.OnRequestStandardInputAtStartup()
        {
            throw new NotSupportedException();
        }

        private class ProcessExecutionEnumerator : IAsyncEnumerator<ProcessResponse>
        {
            private readonly ProcessExecutionEnumerable _enumerable;
            private readonly ResponseQueue _queue;
            private readonly CancellationToken _cancellationToken;
            private ProcessResponse? _current = null;
            private bool _disposed = false;

            public ProcessExecutionEnumerator(
                ProcessExecutionEnumerable enumerable,
                ResponseQueue queue,
                CancellationToken cancellationToken)
            {
                _enumerable = enumerable;
                _queue = queue;
                _enumerable._enumeratorCount++;
                _cancellationToken = cancellationToken;
            }

            public ProcessResponse Current => _current switch
            {
                null => throw new InvalidOperationException("Call MoveNext before accessing Current!"),
                _ => _current,
            };

            public async ValueTask<bool> MoveNextAsync()
            {
                _enumerable.EnsureProcessIsRunning();

                if (_current != null && _current is ExitCodeResponse)
                {
                    // We've already received the exit code.
                    return false;
                }

                if (_enumerable._executingProcess != null &&
                    _enumerable._executingProcess.IsCompleted)
                {
                    // Just pull from the queue, but don't wait on the semaphore since there
                    // may be no more Release calls.
                    if (_queue._responseQueue.TryDequeue(out var result))
                    {
                        if (result is InternalExceptionResponse ex)
                        {
                            throw new InvalidOperationException("An internal exception occurred while executing the process.", ex.Exception);
                        }
                        _current = result;
                        return true;
                    }
                    if (_current == null)
                    {
                        // We must at least provide the ExitCodeResponse.
                        if (_enumerable._exitResponse == null)
                        {
                            throw new InvalidOperationException("Expected to get ExitCodeResponse from parent enumerator.");
                        }
                        _current = _enumerable._exitResponse;
                        return true;
                    }
                    return false;
                }

                {
                    await _queue._responseAvailable.WaitAsync(_cancellationToken);
                    if (!_queue._responseQueue.TryDequeue(out var result))
                    {
                        throw new InvalidOperationException("Expected to pull response from queue when semaphore indicates a message is available!");
                    }
                    if (result is InternalExceptionResponse ex)
                    {
                        throw new InvalidOperationException("An internal exception occurred while executing the process.", ex.Exception);
                    }
                    _current = result;
                    return true;
                }
            }

            public async ValueTask DisposeAsync()
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(ProcessExecutionEnumerator));
                }
                _disposed = true;
                _enumerable._enumeratorCount--;
                if (_enumerable._enumeratorCount == 0 &&
                    _enumerable._executingProcess != null)
                {
                    // There are no more enumerators observing the enumerable,
                    // cancel process execution so it doesn't get left dangling.
                    _enumerable._processCancellationTokenSource.Cancel();
                    try
                    {
                        await _enumerable._executingProcess;
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
        }
    }
}
