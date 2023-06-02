namespace Redpoint.OpenGE.Executor
{
    using Grpc.Core;
    using GrpcDotNetNamedPipes;
    using Microsoft.Extensions.Logging;
    using OpenGEAPI;
    using System;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;

    internal class DefaultOpenGEDaemon : OpenGE.OpenGEBase, IOpenGEDaemon, IDisposable
    {
        private readonly string _pipeName = $"OpenGE-{BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", "").ToLowerInvariant()}";
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly ILogger<DefaultOpenGEDaemon> _logger;
        private readonly IOpenGEExecutorFactory _executorFactory;
        private bool _hasStarted = false;
        private NamedPipeServer? _pipeServer = null;
        private long _inflightJobs = 0;
        private bool _isShuttingDown = false;
        private CancellationToken _shutdownCancellationToken;
        private readonly SemaphoreSlim _inflightJobCountSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _allInflightJobsAreComplete = new SemaphoreSlim(0);

        public DefaultOpenGEDaemon(
            ILogger<DefaultOpenGEDaemon> logger,
            IOpenGEExecutorFactory executorFactory)
        {
            _logger = logger;
            _executorFactory = executorFactory;
        }

        public async Task StartAsync(CancellationToken shutdownCancellationToken)
        {
            _shutdownCancellationToken = shutdownCancellationToken;
            await _semaphore.WaitAsync(shutdownCancellationToken);
            try
            {
                if (_hasStarted)
                {
                    throw new InvalidOperationException();
                }

                _logger.LogInformation($"Starting OpenGE daemon on pipe: {_pipeName}");
                _pipeServer = new NamedPipeServer(_pipeName);
                OpenGE.BindService(_pipeServer.ServiceBinder, this);
                _pipeServer.Start();
                _hasStarted = true;
                _logger.LogInformation($"Started OpenGE daemon on pipe: {_pipeName}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public string GetConnectionString()
        {
            if (!_hasStarted)
            {
                throw new InvalidOperationException();
            }
            return _pipeName;
        }

        private async Task<long> GetInflightJobCount()
        {
            await _inflightJobCountSemaphore.WaitAsync();
            try
            {
                return _inflightJobs;
            }
            finally
            {
                _inflightJobCountSemaphore.Release();
            }
        }

        public async Task StopAsync()
        {
            await _semaphore.WaitAsync(CancellationToken.None);
            try
            {
                _logger.LogTrace("OpenGE in-flight: Starting shutdown");
                _isShuttingDown = true;

                if (_hasStarted)
                {
                    var inFlightCount = await GetInflightJobCount();
                    _logger.LogTrace($"OpenGE in-flight: There are {inFlightCount} jobs in-flight");
                    if (inFlightCount > 0)
                    {
                        _logger.LogTrace("OpenGE in-flight: Waiting for in-flight OpenGE jobs to terminate...");
                        await _allInflightJobsAreComplete.WaitAsync();
                        _logger.LogTrace($"OpenGE in-flight: There are now no jobs in-flight");
                    }

                    _logger.LogInformation($"Stopped OpenGE daemon on pipe: {_pipeName}");
                    _pipeServer!.Kill();
                    _hasStarted = false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public void Dispose()
        {
        }

        public override async Task SubmitJob(SubmitJobRequest request, IServerStreamWriter<SubmitJobResponse> responseStream, ServerCallContext context)
        {
            if (_isShuttingDown)
            {
                // Do not start a job if we're in the process of closing the pipe.
                return;
            }

            // Increment the current job count.
            await _inflightJobCountSemaphore.WaitAsync();
            try
            {
                if (_isShuttingDown)
                {
                    // Do not start a job if we're in the process of closing the pipe.
                    return;
                }
                _logger.LogTrace("OpenGE in-flight: Incremented job count");
                _inflightJobs++;
            }
            finally
            {
                _inflightJobCountSemaphore.Release();
            }

            // Execute the job.
            try
            {
                var globalCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, _shutdownCancellationToken!);

                _logger.LogTrace($"[{request.BuildNodeName}] Received OpenGE job request");

                var st = Stopwatch.StartNew();
                int exitCode;
                IOpenGEExecutor? executor = null;
                try
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.JobXml)))
                    {
                        _logger.LogTrace($"[{request.BuildNodeName}] Executing job request");
                        var buildCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token);
                        executor = _executorFactory.CreateExecutor(stream, buildLogPrefix: $"[{request.BuildNodeName}] ");
                        exitCode = await executor.ExecuteAsync(buildCts);
                        globalCts.Token.ThrowIfCancellationRequested();
                        if (exitCode == 0)
                        {
                            _logger.LogInformation($"[{request.BuildNodeName}] \u001b[32msuccess\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                        }
                        else
                        {
                            _logger.LogInformation($"[{request.BuildNodeName}] \u001b[31mfailure\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                        }
                    }
                    await responseStream.WriteAsync(new SubmitJobResponse
                    {
                        ExitCode = exitCode
                    });
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    if (executor?.CancelledDueToFailure ?? false)
                    {
                        _logger.LogInformation($"[{request.BuildNodeName}] \u001b[31mfailure\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                    }
                    else
                    {
                        _logger.LogInformation($"[{request.BuildNodeName}] \u001b[33mcancelled\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                    }
                    exitCode = 1;

                    if (!globalCts.Token.IsCancellationRequested)
                    {
                        await responseStream.WriteAsync(new SubmitJobResponse
                        {
                            ExitCode = exitCode
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unhandled exception in OpenGE daemon: {ex.Message}");
                }
            }
            finally
            {
                // Decrement the job count.
                await _inflightJobCountSemaphore.WaitAsync();
                try
                {
                    _inflightJobs--;
                    _logger.LogTrace("OpenGE in-flight: Decremented job count");
                    if (_inflightJobs == 0 && _isShuttingDown)
                    {
                        _allInflightJobsAreComplete.Release();
                        _logger.LogTrace("OpenGE in-flight: Firing all in-flight jobs complete semaphore");
                    }
                }
                finally
                {
                    _inflightJobCountSemaphore.Release();
                }
            }
        }
    }
}
