namespace Redpoint.Git.Native
{
    public struct GitFetchProgressInfo
    {
        public string? ServerProgressMessage;
        public int? TotalObjects;
        public int? IndexedObjects;
        public int? ReceivedObjects;
        public long? ReceivedBytes;
        public bool? SlowFetch;
    }
}
