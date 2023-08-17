namespace Redpoint.OpenGE.Component.Worker
{
    using Redpoint.ApplicationLifecycle;
    using Redpoint.OpenGE.Protocol;

    public interface IWorkerComponent : IApplicationLifecycle
    {
        TaskApi.TaskApiBase TaskApi { get; }

        int? ListeningPort { get; }

        string WorkerDisplayName { get; }

        string WorkerUniqueId { get; }
    }
}
