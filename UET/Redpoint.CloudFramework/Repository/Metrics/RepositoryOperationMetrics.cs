namespace Redpoint.CloudFramework.Repository.Metrics
{
    /// <summary>
    /// If you construct this class and pass an instance to a repository or global repository call, it will return
    /// metrics about the call, such as whether or not the cache was hit.
    /// </summary>
    public class RepositoryOperationMetrics
    {
        /// <summary>
        /// Did we read from the Redis cache for this query?
        /// </summary>
        /// <remarks>
        /// An operation can both read and write to the cache during LoadAsync (if only some keys are cached).
        /// </remarks>
        public bool CacheDidRead { get; internal set; }

        /// <summary>
        /// Did we write from the Redis cache as part of this query?
        /// </summary>
        /// <remarks>
        /// An operation can both read and write to the cache during LoadAsync (if only some keys are cached).
        /// </remarks>
        public bool CacheDidWrite { get; internal set; }

        /// <summary>
        /// Is this query ever compatible with the cache? If this is false, then this query
        /// will always hit Datastore for reads.
        /// </summary>
        public bool CacheCompatible { get; internal set; }

        /// <summary>
        /// The number of cached queries (both singular and complex) that were 
        /// flushed from the Redis cache due to this write operation.
        /// </summary>
        public long CacheQueriesFlushed { get; internal set; }

        /// <summary>
        /// For QueryAsync operations, this returns the query hash used to key result sets inside Redis.
        /// </summary>
        public string? CacheHash { get; internal set; }

        /// <summary>
        /// The number of milliseconds elapsed at the Redis caching layer. This excludes
        /// time spent in the Datastore layer.
        /// </summary>
        public float CacheElapsedMilliseconds { get; internal set; }

        /// <summary>
        /// The number of entities read directly from Datastore.
        /// </summary>
        public long DatastoreEntitiesRead { get; internal set; }

        /// <summary>
        /// The number of entities written directly to Datastore.
        /// </summary>
        public long DatastoreEntitiesWritten { get; internal set; }

        /// <summary>
        /// The number of entities deleted directly from Datastore.
        /// </summary>
        public long DatastoreEntitiesDeleted { get; internal set; }

        /// <summary>
        /// The number of milliseconds elapsed at the Datastore layer.
        /// </summary>
        public float DatastoreElapsedMilliseconds { get; internal set; }
    }
}
