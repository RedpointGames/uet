namespace Redpoint.OpenGE.Component.Worker
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System.Net;
    using System.Threading.Tasks;

    internal interface IExecutionManager
    {
        Task ExecuteTaskAsync(
            IPAddress peerAddress,
            ExecuteTaskRequest request,
            IServerStreamWriter<ExecutionResponse> responseStream,
            CancellationToken cancellationToken);
    }
}
