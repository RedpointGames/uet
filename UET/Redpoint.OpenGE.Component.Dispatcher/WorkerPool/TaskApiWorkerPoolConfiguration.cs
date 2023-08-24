namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public class TaskApiWorkerPoolConfiguration
    {
        public required bool EnableNetworkAutoDiscovery { get; set; }
        public required TaskApiWorkerPoolConfigurationLocalWorker? LocalWorker { get; set; }
    }
}
