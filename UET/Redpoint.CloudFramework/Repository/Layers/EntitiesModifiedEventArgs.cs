namespace Redpoint.CloudFramework.Repository.Layers
{
    using Redpoint.CloudFramework.Models;
    using Redpoint.CloudFramework.Repository.Metrics;
    using System;

    internal class EntitiesModifiedEventArgs : EventArgs
    {
        /// <summary>
        /// The keys that were modified or deleted.
        /// </summary>
        public required UntypedKey[] Keys { get; init; }

        /// <summary>
        /// If there were metrics passed into the original operation that caused this event to be
        /// raised, this is the metrics object that was passed in.
        /// </summary>
        public required RepositoryOperationMetrics? Metrics { get; init; }
    }
}
