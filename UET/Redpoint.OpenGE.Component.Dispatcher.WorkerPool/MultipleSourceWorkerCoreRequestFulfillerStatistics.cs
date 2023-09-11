namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Collections.Generic;

    public class MultipleSourceWorkerCoreRequestFulfillerStatistics<TWorkerCore>
    {
        public required IReadOnlyDictionary<IWorkerCoreProvider<TWorkerCore>, MultipleSourceWorkerCoreRequestFulfillerStatisticsForCore<TWorkerCore>> Providers;
    }
}
