namespace Redpoint.Uet.Workspace.Storage
{
    using System.Collections.Generic;

    public record struct ListStorageResult
    {
        public required IReadOnlyList<StorageEntry> Entries { get; init; }
        public required int MaxIdLength { get; init; }
        public required int MaxPathLength { get; init; }
        public required int MaxTypeLength { get; init; }
    }
}
