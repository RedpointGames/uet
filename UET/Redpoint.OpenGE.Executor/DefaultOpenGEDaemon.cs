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

        public DefaultOpenGEDaemon(
            ILogger<DefaultOpenGEDaemon> logger,
            IOpenGEExecutorFactory executorFactory)
        {
            _logger = logger;
            _executorFactory = executorFactory;
        }

        public async Task<string> StartIfNeededAndGetConnectionEnvironmentVariableAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_hasStarted)
                {
                    return _pipeName;
                }

                _logger.LogInformation($"Starting OpenGE daemon on pipe: {_pipeName}");
                _pipeServer = new NamedPipeServer(_pipeName);
                OpenGE.BindService(_pipeServer.ServiceBinder, this);
                _pipeServer.Start();
                _hasStarted = true;
                _logger.LogInformation($"Started OpenGE daemon on pipe: {_pipeName}");
                return _pipeName;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                if (_hasStarted)
                {
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
            _logger.LogTrace($"[{request.BuildNodeName}] Received OpenGE job request");

            var st = Stopwatch.StartNew();
            int exitCode;
            try
            {
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(request.JobXml)))
                {
                    _logger.LogTrace($"[{request.BuildNodeName}] Executing job request");
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                    var executor = _executorFactory.CreateExecutor(stream, buildLogPrefix: $"[{request.BuildNodeName}] ");
                    exitCode = await executor.ExecuteAsync(cts);
                    context.CancellationToken.ThrowIfCancellationRequested();
                    if (exitCode == 0)
                    {
                        _logger.LogInformation($"[{request.BuildNodeName}] \u001b[32msuccess\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                    }
                    else
                    {
                        _logger.LogInformation($"[{request.BuildNodeName}] \u001b[31mfailure\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                    }
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogInformation($"[{request.BuildNodeName}] \u001b[33mcancelled\u001b[0m in {st.Elapsed.TotalSeconds:F2} secs");
                exitCode = 1;
            }
            if (!context.CancellationToken.IsCancellationRequested)
            {
                await responseStream.WriteAsync(new SubmitJobResponse
                {
                    ExitCode = exitCode
                });
            }
        }
    }
}
