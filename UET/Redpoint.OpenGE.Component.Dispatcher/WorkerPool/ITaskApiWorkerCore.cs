namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Core;
    using Redpoint.OpenGE.Protocol;

    public interface ITaskApiWorkerCore : IWorkerCoreWithLiveness
    {
        string WorkerMachineName { get; }

        int WorkerCoreNumber { get; }

        string WorkerCoreUniqueAssignmentId { get; }

        BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request { get; }
    }
}
