namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Protocol;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class DefaultDispatcherComponent : JobApi.JobApiBase, IDispatcherComponent
    {
        private readonly string _pipeName;
        private readonly GrpcPipeNamespace _pipeNamespace;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1);
        private readonly ILogger<DefaultDispatcherComponent> _logger;
        private readonly IGraphExecutorFactory _executorFactory;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private bool _hasStarted = false;
        private IGrpcPipeServer<DefaultDispatcherComponent>? _pipeServer = null;
        private long _inflightJobs = 0;
        private bool _isShuttingDown = false;
        private CancellationToken _shutdownCancellationToken;
        private readonly SemaphoreSlim _inflightJobCountSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _allInflightJobsAreComplete = new SemaphoreSlim(0);

        public DefaultDispatcherComponent(
            ILogger<DefaultDispatcherComponent> logger,
            IGraphExecutorFactory executorFactory,
            IGrpcPipeFactory grpcPipeFactory,
            string? pipeName)
        {
            _logger = logger;
            _executorFactory = executorFactory;
            _grpcPipeFactory = grpcPipeFactory;
            _pipeName = pipeName ?? $"OpenGE-{BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", "").ToLowerInvariant()}";
            _pipeNamespace = pipeName == null ? GrpcPipeNamespace.Computer : GrpcPipeNamespace.User;
        }

        public JobApi.JobApiBase JobApi => this;

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

                _logger.LogTrace($"Starting OpenGE daemon on pipe: {_pipeName}");
                _pipeServer = _grpcPipeFactory.CreateServer(
                    _pipeName,
                    _pipeNamespace,
                    this);
                await _pipeServer.StartAsync();
                _hasStarted = true;
                _logger.LogTrace($"Started OpenGE daemon on pipe: {_pipeName}");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public bool IsDaemonRunning => _hasStarted;

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

                    _logger.LogTrace($"Stopped OpenGE daemon on pipe: {_pipeName}");
                    await _pipeServer!.StopAsync();
                    _hasStarted = false;
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public override async Task SubmitJob(SubmitJobRequest request, IServerStreamWriter<JobResponse> responseStream, ServerCallContext context)
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
                //int exitCode = 1;
                IGraphExecutor? executor = null;
                try
                {
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.JobXml)))
                    {
                        _logger.LogTrace($"[{request.BuildNodeName}] Executing job request");
                        var buildCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token);
                        var envs = new Dictionary<string, string>();
                        foreach (var kv in request.EnvironmentVariables)
                        {
                            envs[kv.Key] = kv.Value;
                        }
                        executor = _executorFactory.CreateGraphExecutor(
                            stream,
                            envs,
                            request.WorkingDirectory,
                            request.BuildNodeName);
                        await executor.ExecuteAsync(
                            responseStream,
                            buildCts);
                        globalCts.Token.ThrowIfCancellationRequested();
                    }
                    /*
                    await responseStream.WriteAsync(new JobResponse
                    {
                        JobComplete = new JobCompleteResponse
                        {
                            Status = exitCode == 0 ? JobCompletionStatus.Success : JobCompletionStatus.Failure,
                            ExitCode = exitCode,
                            TotalSeconds = st.Elapsed.TotalSeconds,
                        }
                    });
                    */
                    /*
                    if ()
                    {
                        _logger.LogInformation($"[{request.BuildNodeName}] {Bright.Green("success")} in {st.Elapsed.TotalSeconds:F2} secs");
                    }
                    else
                    {
                        _logger.LogInformation($"[{request.BuildNodeName}] {Bright.Red("failure")} in {st.Elapsed.TotalSeconds:F2} secs");
                    }
                    */
                }
                catch (Exception ex) when (ex is OperationCanceledException)
                {
                    //exitCode = 1;
                    if (!globalCts.Token.IsCancellationRequested)
                    {
                        if (executor?.CancelledDueToFailure ?? false)
                        {
                            await responseStream.WriteAsync(new JobResponse
                            {
                                JobComplete = new JobCompleteResponse
                                {
                                    Status = JobCompletionStatus.Failure,
                                    ExitCode = 1,
                                    TotalSeconds = st.Elapsed.TotalSeconds,
                                }
                            });
                            //_logger.LogInformation($"[{request.BuildNodeName}] {Bright.Red("failure")} in {st.Elapsed.TotalSeconds:F2} secs");
                        }
                        else
                        {
                            await responseStream.WriteAsync(new JobResponse
                            {
                                JobComplete = new JobCompleteResponse
                                {
                                    Status = JobCompletionStatus.Cancelled,
                                    ExitCode = 1,
                                    TotalSeconds = st.Elapsed.TotalSeconds,
                                }
                            });
                            //_logger.LogInformation($"[{request.BuildNodeName}] {Bright.Yellow("cancelled")} in {st.Elapsed.TotalSeconds:F2} secs");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Unhandled exception in OpenGE dispatcher component: {ex.Message}");
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
