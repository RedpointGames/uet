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
                var didGetExitCode = false;
                await foreach (var response in processResponseStream)
                {
                    if (didGetExitCode)
                    {
                        throw new RpcException(new Status(StatusCode.Internal, "Task executor sent response after sending ExitCode."));
                    }
                    await responseStream.WriteAsync(new ExecutionResponse
                    {
                        ExecuteTask = new ExecuteTaskResponse
                        {
                            Response = response,
                        },
                    });
                    if (response.DataCase == ProcessResponse.DataOneofCase.ExitCode)
                    {
                        didGetExitCode = true;
                    }
                }
                if (!didGetExitCode)
                {
                    throw new RpcException(new Status(StatusCode.Internal, "All task executors must emit an ExitCode as their final response."));
                }
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
    }
}
