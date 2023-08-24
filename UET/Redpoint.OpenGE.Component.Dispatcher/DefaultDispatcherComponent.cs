namespace Redpoint.OpenGE.Component.Dispatcher
{
    using Grpc.Core;
    using Microsoft.Extensions.Logging;
    using Redpoint.GrpcPipes;
    using Redpoint.OpenGE.Component.Dispatcher.Graph;
    using Redpoint.OpenGE.Component.Dispatcher.GraphExecutor;
    using Redpoint.OpenGE.Component.Dispatcher.GraphGenerator;
    using Redpoint.OpenGE.Component.Dispatcher.WorkerPool;
    using Redpoint.OpenGE.JobXml;
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
        private readonly IGraphGenerator _graphGenerator;
        private readonly IGraphExecutor _graphExecutor;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly ITaskApiWorkerPool _workerPool;
        private bool _hasStarted = false;
        private IGrpcPipeServer<DefaultDispatcherComponent>? _pipeServer = null;
        private long _inflightJobs = 0;
        private bool _isShuttingDown = false;
        private CancellationToken _shutdownCancellationToken;
        private readonly SemaphoreSlim _inflightJobCountSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _allInflightJobsAreComplete = new SemaphoreSlim(0);

        public DefaultDispatcherComponent(
            ILogger<DefaultDispatcherComponent> logger,
            IGraphGenerator graphGenerator,
            IGraphExecutor graphExecutor,
            IGrpcPipeFactory grpcPipeFactory,
            ITaskApiWorkerPool workerPool,
            string? pipeName)
        {
            _logger = logger;
            _graphGenerator = graphGenerator;
            _graphExecutor = graphExecutor;
            _grpcPipeFactory = grpcPipeFactory;
            _workerPool = workerPool;
            _pipeName = pipeName ?? $"OpenGE-{BitConverter.ToString(Guid.NewGuid().ToByteArray()).Replace("-", "").ToLowerInvariant()}";
            _pipeNamespace = pipeName == null ? GrpcPipeNamespace.User : GrpcPipeNamespace.Computer;
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

        public override Task<PingJobServiceResponse> PingJobService(PingJobServiceRequest request, ServerCallContext context)
        {
            return Task.FromResult(new PingJobServiceResponse());
        }

        public override async Task SubmitJob(
            SubmitJobRequest request,
            IServerStreamWriter<JobResponse> unsafeResponseStream,
            ServerCallContext context)
        {
            var responseStream = new GuardedResponseStream<JobResponse>(unsafeResponseStream);

            try
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
                    var globalCts = CancellationTokenSource.CreateLinkedTokenSource(
                        context.CancellationToken,
                        _shutdownCancellationToken!);

                    _logger.LogTrace($"[{request.BuildNodeName}] Received OpenGE job request");

                    try
                    {
                        Graph.Graph graph;
                        using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.JobXml)))
                        {
                            _logger.LogTrace($"[{request.BuildNodeName}] Executing job request");

                            // Convert the environment variables from the gRPC map.
                            var envs = new Dictionary<string, string>();
                            foreach (var kv in request.EnvironmentVariables)
                            {
                                envs[kv.Key] = kv.Value;
                            }

                            // Convert the graph XML into the graph object, which describes
                            // all of the tasks to run and their dependencies.
                            graph = await _graphGenerator.GenerateGraphFromJobAsync(
                                JobXmlReader.ParseJobXml(stream),
                                new GraphExecutionEnvironment
                                {
                                    EnvironmentVariables = envs,
                                    WorkingDirectory = request.WorkingDirectory,
                                    BuildStartTicks = DateTimeOffset.UtcNow.Ticks,
                                },
                                globalCts.Token);
                        }

                        // Tell the client how many tasks we're about to run.
                        await responseStream.WriteAsync(new JobResponse
                        {
                            JobParsed = new JobParsedResponse
                            {
                                TotalTasks = graph.Tasks.Count(x => x.Value is not DescribingGraphTask),
                            }
                        });

                        // If there are no tasks, finish immediately.
                        if (graph.Tasks.Count == 0)
                        {
                            await responseStream.WriteAsync(new JobResponse
                            {
                                JobComplete = new JobCompleteResponse
                                {
                                    Status = JobCompletionStatus.JobCompletionSuccess,
                                    TotalSeconds = 0,
                                }
                            });
                            return;
                        }

                        _logger.LogTrace("Graph execution starting...");
                        await _graphExecutor.ExecuteGraphAsync(
                            _workerPool,
                            graph,
                            request.BuildBehaviour,
                            responseStream,
                            context.CancellationToken);
                        _logger.LogTrace("Graph execution completed without throwing an exception");
                    }
                    catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                    {
                        // The operation is being cancelled.
                        _logger.LogTrace("RPC exception cancellation");
                    }
                    catch (OperationCanceledException ex)
                    {
                        // The requester cancelled the RPC; this is normal. We can't send responses
                        // back in this state so we just gracefully exit the call to prevent this
                        // error being logged.
                        _logger.LogTrace("Operation cancelled: " + ex.ToString());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Unhandled exception in OpenGE dispatcher component: {ex.Message}");
                        throw new RpcException(new Status(
                            StatusCode.Internal,
                            ex.ToString()));
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
            catch (Exception ex)
            {
                _logger.LogWarning("Exception escaping SubmitJob, which is a bug! " + ex);
                throw;
            }
        }
    }
}
