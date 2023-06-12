namespace Redpoint.Git.Native
{
    /// <summary>
    /// Represent process of a 'git fetch' operation.
    /// </summary>
    public struct GitFetchProgressInfo
    {
        /// <summary>
        /// The progress message from the remote Git server. <c>null</c> until the server reports it.
        /// </summary>
        public string? ServerProgressMessage;

        /// <summary>
        /// The total number of Git objects. <c>null</c> until the server reports it.
        /// </summary>
        public int? TotalObjects;

        /// <summary>
        /// The number of Git objects indexed so far. <c>null</c> until the server reports it.
        /// </summary>
        public int? IndexedObjects;

        /// <summary>
        /// The number of Git objects received so far. <c>null</c> until the server reports it.
        /// </summary>
        public int? ReceivedObjects;

        /// <summary>
        /// The number of bytes received so far. <c>null</c> until the server reports it.
        /// </summary>
        public long? ReceivedBytes;

        /// <summary>
        /// If true, the fetch is being performed with libgit2 instead of the Git executable.
        /// </summary>
        public bool? SlowFetch;
    }
}
