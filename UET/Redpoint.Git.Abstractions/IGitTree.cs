namespace Redpoint.Git.Abstractions
{
    using Redpoint.Vfs.Abstractions;

    public interface IGitTree
    {
        public record GitVfsEntry : VfsEntry
        {
            public required string? BlobSha { get; init; }

            public required string AbsoluteParentPath { get; init; }

            public required string AbsolutePath { get; init; }
        }

        public class GitTreeEnumerationMetrics
        {
            private readonly bool _emitAt10000;
            private long _objectsMapped = 0;

            public GitTreeEnumerationMetrics(bool emitAt10000 = false)
            {
                _emitAt10000 = emitAt10000;
            }

            public long ObjectsMapped
            {
                get => _objectsMapped;
                set
                {
                    _objectsMapped = value;

                    // @hack: We should improve this.
                    if (_emitAt10000 && _objectsMapped % 10000 == 0)
                    {
                        Console.WriteLine($"Git parsed objects: {_objectsMapped}");
                    }
                }
            }
        }

        string Sha { get; }

        IAsyncEnumerable<GitVfsEntry> EnumerateRecursivelyAsync(GitTreeEnumerationMetrics? metrics, CancellationToken cancellationToken);
    }
}
