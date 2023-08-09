namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Component.Worker.TaskDescriptorExecutors;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal class DefaultExecutionManager : IExecutionManager
    {
        private readonly ITaskDescriptorExecutor<LocalTaskDescriptor> _localTaskExecutor;
        private readonly ITaskDescriptorExecutor<CopyTaskDescriptor> _copyTaskExecutor;

        public DefaultExecutionManager(
            ITaskDescriptorExecutor<LocalTaskDescriptor> localTaskExecutor,
            ITaskDescriptorExecutor<CopyTaskDescriptor> copyTaskExecutor)
        {
            _localTaskExecutor = localTaskExecutor;
            _copyTaskExecutor = copyTaskExecutor;
        }

        public async Task ExecuteTaskAsync(
            ExecuteTaskRequest request, 
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken)
        {
            try
            {
                var shouldRestart = false;
                var restartingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken);
                var autoRecover = new List<string>(request.AutoRecover);
                // @hack: Do this in a better place.
                if (request.Descriptor_.DescriptorCase == TaskDescriptor.DescriptorOneofCase.Local)
                {
                    if (Path.GetFileName(request.Descriptor_.Local.Path) == "cl.exe")
                    {
                        // "c1xx : fatal error C1356: unable to find mspdbcore.dll"
                        // which can happen under high loads.
                        autoRecover.Add("C1356");
                        // "cl : Command line error D8037: cannot create temporary il file; clean temp directory of old il files"
                        // which can happen under high loads.
                        autoRecover.Add("D8037");
                    }
                    else if (Path.GetFileName(request.Descriptor_.Local.Path) == "link.exe")
                    {
                        // "LINK : fatal error LNK1171: unable to load mspdbcore.dll (error code: 1455)"
                        // which can happen under high loads.
                        autoRecover.Add("LNK1171");
                    }
                }
                do
                {
                    shouldRestart = false;
                    var processResponseStream = GetProcessResponseStreamFromRequest(
                        request, 
                        restartingCancellationTokenSource.Token);
                    var didGetExitCode = false;
                    await foreach (var response in processResponseStream)
                    {
                        if (didGetExitCode)
                        {
                            throw new RpcException(new Status(StatusCode.Internal, "Task executor sent response after sending ExitCode."));
                        }
                        var shouldAutoRecover = false;
                        if (autoRecover.Count > 0)
                        {
                            switch (response.DataCase)
                            {
                                case ProcessResponse.DataOneofCase.StandardOutputLine:
                                    if (autoRecover.Any(x => response.StandardOutputLine.Contains(x)))
                                    {
                                        shouldAutoRecover = true;
                                    }
                                    break;
                                case ProcessResponse.DataOneofCase.StandardErrorLine:
                                    if (autoRecover.Any(x => response.StandardErrorLine.Contains(x)))
                                    {
                                        shouldAutoRecover = true;
                                    }
                                    break;
                            }
                        }
                        if (shouldAutoRecover)
                        {
                            // We must auto-recover and restart the process.
                            restartingCancellationTokenSource.Cancel();
                            restartingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                                cancellationToken);
                            shouldRestart = true;
                            break;
                        }
                        var ignoreThisOutputLine = false;
                        switch (response.DataCase)
                        {
                            case ProcessResponse.DataOneofCase.StandardOutputLine:
                                if (request.IgnoreLines.Any(x => response.StandardOutputLine.Contains(x)))
                                {
                                    ignoreThisOutputLine = true;
                                }
                                break;
                            case ProcessResponse.DataOneofCase.StandardErrorLine:
                                if (request.IgnoreLines.Any(x => response.StandardErrorLine.Contains(x)))
                                {
                                    ignoreThisOutputLine = true;
                                }
                                break;
                        }
                        if (!ignoreThisOutputLine)
                        {
                            await responseStream.WriteAsync(new ExecutionResponse
                            {
                                ExecuteTask = new ExecuteTaskResponse
                                {
                                    Response = response,
                                },
                            });
                        }
                        if (response.DataCase == ProcessResponse.DataOneofCase.ExitCode)
                        {
                            didGetExitCode = true;
                        }
                    }
                    if (!didGetExitCode && !shouldRestart)
                    {
                        throw new RpcException(new Status(StatusCode.Internal, "All task executors must emit an ExitCode as their final response."));
                    }
                } while (shouldRestart);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new RpcException(new Status(StatusCode.Internal, ex.ToString()));
            }
        }

        private IAsyncEnumerable<ProcessResponse> GetProcessResponseStreamFromRequest(ExecuteTaskRequest request, CancellationToken cancellationToken)
        {
            IAsyncEnumerable<ProcessResponse> processResponseStream;
            switch (request.Descriptor_.DescriptorCase)
            {
                case TaskDescriptor.DescriptorOneofCase.Local:
                    processResponseStream = _localTaskExecutor.ExecuteAsync(
                        request.Descriptor_.Local,
                        cancellationToken);
                    break;
                case TaskDescriptor.DescriptorOneofCase.Copy:
                    processResponseStream = _copyTaskExecutor.ExecuteAsync(
                        request.Descriptor_.Copy,
                        cancellationToken);
                    break;
                case TaskDescriptor.DescriptorOneofCase.Remote:
                default:
                    throw new RpcException(new Status(StatusCode.Unimplemented, "No executor for this descriptor type."));
            }

            return processResponseStream;
        }
    }
}
