namespace Redpoint.Git.Native
{
    using LibGit2Sharp;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Redpoint.Git.Abstractions;
    using static Redpoint.Git.Abstractions.IGitTree;

    internal class NativeGitTree : IGitTree
    {
        private readonly Repository _repository;
        private readonly Tree _tree;
        private readonly DateTimeOffset _committedAtUtc;

        public NativeGitTree(
            Repository repository,
            Tree tree,
            DateTimeOffset committedAtUtc)
        {
            _repository = repository;
            _tree = tree;
            _committedAtUtc = committedAtUtc;
        }

        public string Sha => _tree.Sha;

        private IEnumerable<GitVfsEntry> ProcessTree(
            string path,
            Tree tree,
            GitTreeEnumerationMetrics? metrics)
        {
            foreach (var entry in tree)
            {
                var subpath = (path + Path.DirectorySeparatorChar + entry.Name).TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
                switch (entry.TargetType)
                {
                    case TreeEntryTargetType.Tree:
                        var treeEntry = new GitVfsEntry
                        {
                            Name = entry.Name,
                            CreationTime = _committedAtUtc,
                            LastAccessTime = _committedAtUtc,
                            LastWriteTime = _committedAtUtc,
                            ChangeTime = _committedAtUtc,
                            Attributes = FileAttributes.Directory,
                            Size = 0,
                            BlobSha = null,
                            AbsoluteParentPath = path,
                            AbsolutePath = subpath,
                        };
                        if (metrics != null)
                        {
                            metrics.ObjectsMapped++;
                        }
                        yield return treeEntry;
                        foreach (var subentry in ProcessTree(subpath, (Tree)entry.Target, metrics))
                        {
                            yield return subentry;
                        }
                        break;
                    case TreeEntryTargetType.Blob:
                        var blob = (Blob)entry.Target;
                        var blobEntry = new GitVfsEntry
                        {
                            Name = entry.Name,
                            CreationTime = _committedAtUtc,
                            LastAccessTime = _committedAtUtc,
                            LastWriteTime = _committedAtUtc,
                            ChangeTime = _committedAtUtc,
                            Attributes = FileAttributes.Archive,
                            Size = _repository.ObjectDatabase.RetrieveObjectMetadata(blob.Id).Size,
                            AbsoluteParentPath = path,
                            AbsolutePath = subpath,
                            BlobSha = blob.Sha,
                        };
                        if (metrics != null)
                        {
                            metrics.ObjectsMapped++;
                        }
                        yield return blobEntry;
                        break;
                    case TreeEntryTargetType.GitLink:
                        // No support for projecting submodules; Unreal Engine doesn't use them.
                        break;
                }
            }
        }

        public IAsyncEnumerable<GitVfsEntry> EnumerateRecursivelyAsync(GitTreeEnumerationMetrics? metrics, CancellationToken cancellationToken)
        {
            return ProcessTree(string.Empty, _tree, metrics).ToAsyncEnumerable();
        }
    }
}
