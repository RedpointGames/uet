namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Threading.Tasks;

    internal interface IExecutionManager
    {
        Task ExecuteTaskAsync(
            ExecuteTaskRequest request,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken);
    }
}
