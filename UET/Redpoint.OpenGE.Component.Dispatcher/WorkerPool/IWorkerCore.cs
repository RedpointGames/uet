namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Grpc.Core;
    using Redpoint.OpenGE.Protocol;
    using System;

    public interface IWorkerCore : IAsyncDisposable
    {
        string WorkerMachineName { get; }

        int WorkerCoreNumber { get; }

        bool Dead { get; }

        BufferedAsyncDuplexStreamingCall<ExecutionRequest, ExecutionResponse> Request { get; }
    }
}
