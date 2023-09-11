namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    public class SingleSourceWorkerCoreRequestFulfillerStatistics<TWorkerCore>
    {
        public required long CoreAcquiringCount;
        public required int CoresCurrentlyAcquiredCount;
        public required TWorkerCore[] CoresCurrentlyAcquired;
    }
}
