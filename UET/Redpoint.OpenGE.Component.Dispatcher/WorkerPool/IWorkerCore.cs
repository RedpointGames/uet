namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System;

    public interface IWorkerCore : IAsyncDisposable
    {
        string WorkerMachineName { get; }

        int WorkerCoreNumber { get; }

        AsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request { get; }
    }
}
