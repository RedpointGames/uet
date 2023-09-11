namespace UET.Commands.Storage
{
    internal sealed class StorageEntry
    {
        public required string Id;
        public required string Path;
        public required StorageEntryType Type;
        public required DateTimeOffset LastUsed;
        public required ulong DiskSpaceConsumed;
    }
}
