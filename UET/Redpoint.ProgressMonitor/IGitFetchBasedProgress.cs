namespace Redpoint.ProgressMonitor
{
    /// <summary>
    /// Represents a 'git fetch' progress object that returns information about the Git operation to report on.
    /// </summary>
    public interface IGitFetchBasedProgress
    {
        /// <summary>
        /// The total Git objects in this fetch operation.
        /// </summary>
        int? TotalObjects { get; }

        /// <summary>
        /// The number of Git objects received so far.
        /// </summary>
        int? ReceivedObjects { get; }

        /// <summary>
        /// The number of bytes received so far.
        /// </summary>
        long? ReceivedBytes { get; }

        /// <summary>
        /// The number of Git objects indexed so far.
        /// </summary>
        int? IndexedObjects { get; }

        /// <summary>
        /// If set, appears before the progress information.
        /// </summary>
        string? FetchContext { get; }

        /// <summary>
        /// If set and there's no object information, appears in place of the object infromation.
        /// </summary>
        string? ServerProgressMessage { get; }

        /// <summary>
        /// If set, indicates how "heavy" indexing is compared to fetching. This is used to weight
        /// the percentage progress during the operation. If not set, defaults to 0.1.
        /// 
        /// A value of 0.1 indicates a 1:9 ratio, where indexing is 10% and fetching is 90%.
        /// A value of 1.0 indicates a 1:1 ratio, where indexing is 50% and fetching is 50%.
        /// A value of 9.0 indicates a 9:1 ratio, where indexing is 90% and fetching is 10%.
        /// </summary>
        double? IndexingProgressWeight { get; }
    }
}