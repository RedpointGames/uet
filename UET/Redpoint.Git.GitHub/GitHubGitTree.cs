namespace Redpoint.Git.GitHub
{
    using Octokit;
    using Redpoint.Git.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using static Redpoint.Git.Abstractions.IGitTree;

    internal class GitHubGitTree : IGitTree
    {
        private GitHubClient _client;
        private string _owner;
        private string _repo;
        private string _sha;
        private readonly DateTimeOffset _committedAtUtc;

        public GitHubGitTree(GitHubClient client, string owner, string repo, string sha, DateTimeOffset committedAtUtc)
        {
            _client = client;
            _owner = owner;
            _repo = repo;
            _sha = sha;
            _committedAtUtc = committedAtUtc;
        }

        public string Sha => _sha;

        private GitVfsEntry? ConvertTreeEntry(
            string parentPath,
            TreeItem entry,
            GitTreeEnumerationMetrics? metrics)
        {
            var normalizedPath = entry.Path.Replace('/', Path.DirectorySeparatorChar);
            var name = Path.GetFileName(normalizedPath);
            var directory = Path.GetDirectoryName(normalizedPath);
            string absoluteParentPath, absolutePath;
            if (!string.IsNullOrWhiteSpace(directory))
            {
                absoluteParentPath = (parentPath + Path.DirectorySeparatorChar + directory).TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
                absolutePath = (absoluteParentPath + Path.DirectorySeparatorChar + name).TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
            }
            else
            {
                absoluteParentPath = parentPath.TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
                absolutePath = (absoluteParentPath + Path.DirectorySeparatorChar + name).TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
            }

            switch (entry.Type.Value)
            {
                case TreeType.Tree:
                    var treeEntry = new GitVfsEntry
                    {
                        Name = name,
                        CreationTime = _committedAtUtc,
                        LastAccessTime = _committedAtUtc,
                        LastWriteTime = _committedAtUtc,
                        ChangeTime = _committedAtUtc,
                        Attributes = FileAttributes.Directory,
                        Size = 0,
                        BlobSha = null,
                        AbsoluteParentPath = absoluteParentPath,
                        AbsolutePath = absolutePath,
                    };
                    if (metrics != null)
                    {
                        metrics.ObjectsMapped++;
                    }
                    return treeEntry;
                case TreeType.Blob:
                    var blobEntry = new GitVfsEntry
                    {
                        Name = name,
                        CreationTime = _committedAtUtc,
                        LastAccessTime = _committedAtUtc,
                        LastWriteTime = _committedAtUtc,
                        ChangeTime = _committedAtUtc,
                        Attributes = FileAttributes.Archive,
                        Size = entry.Size,
                        BlobSha = entry.Sha,
                        AbsoluteParentPath = absoluteParentPath,
                        AbsolutePath = absolutePath,
                    };
                    if (metrics != null)
                    {
                        metrics.ObjectsMapped++;
                    }
                    return blobEntry;
                case TreeType.Commit:
                    // No support for projecting submodules; Unreal Engine doesn't use them.
                    return null;
            }

            throw new NotSupportedException($"Unsupported tree entry type: {entry.Type}");
        }

        private async IAsyncEnumerable<GitVfsEntry> EnumerateRecursivelyAsync(
            string sha,
            string path,
            IGitTree.GitTreeEnumerationMetrics? metrics,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var fullTree = await _client.Git.Tree.GetRecursive(_owner, _repo, sha);
            if (fullTree.Truncated)
            {
                // We couldn't recursively get the whole tree in a single call. Instead
                // do a non-recursive API call for this tree, and then enumerate through
                // the directories that are returned to 
                var nonRecursiveTree = await _client.Git.Tree.Get(_owner, _repo, sha);
                foreach (var entry in nonRecursiveTree.Tree)
                {
                    // Emit for the entry itself.
                    var translatedEntry = ConvertTreeEntry(path, entry, metrics);
                    if (translatedEntry != null)
                    {
                        yield return translatedEntry;

                        // Recursive into this directory.
                        if (entry.Type == TreeType.Tree)
                        {
                            await foreach (var subentry in EnumerateRecursivelyAsync(
                                entry.Sha,
                                path + Path.DirectorySeparatorChar + entry.Path,
                                metrics,
                                cancellationToken))
                            {
                                yield return subentry;
                            }
                        }
                    }
                }
            }
            else
            {

                // This is a full view of all the objects underneath this path. Simply
                // translate the entries and return.
                foreach (var entry in fullTree.Tree)
                {
                    var translatedEntry = ConvertTreeEntry(path, entry, metrics);
                    if (translatedEntry != null)
                    {
                        yield return translatedEntry;
                    }
                }
            }
        }

        public IAsyncEnumerable<GitVfsEntry> EnumerateRecursivelyAsync(
            GitTreeEnumerationMetrics? metrics,
            CancellationToken cancellationToken)
        {
            return EnumerateRecursivelyAsync(
                _sha,
                string.Empty,
                metrics,
                cancellationToken);
        }
    }
}
