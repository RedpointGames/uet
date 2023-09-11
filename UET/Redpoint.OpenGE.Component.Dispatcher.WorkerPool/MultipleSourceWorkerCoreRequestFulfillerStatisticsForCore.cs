namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public class MultipleSourceWorkerCoreRequestFulfillerStatisticsForCore<TWorkerCore>
    {
        public required string UniqueId;
        public required bool IsObtainingCore;
        public required TWorkerCore? ObtainedCore;
    }
}
