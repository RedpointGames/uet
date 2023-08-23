namespace Redpoint.OpenGE.Component.Dispatcher.WorkerPool
{
    using System.Collections.Generic;

    internal record class WorkerCoreProviderCollectionChanged<TWorkerCore>
    {
        public required IReadOnlyList<IWorkerCoreProvider<TWorkerCore>> CurrentProviders { get; init; }

        public IWorkerCoreProvider<TWorkerCore>? AddedProvider { get; init; }

        public IWorkerCoreProvider<TWorkerCore>? RemovedProvider { get; init; }
    }
}
