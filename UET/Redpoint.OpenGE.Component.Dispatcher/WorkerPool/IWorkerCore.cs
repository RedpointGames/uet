namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using Redpoint.OpenGE.Protocol;
    using System;

    internal interface IWorkerCore : IAsyncDisposable
    {
        TaskApi.TaskApiClient Client { get; }
    }
}
