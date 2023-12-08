namespace Redpoint.Vfs.Layer.Git
{
    using Redpoint.Git.Abstractions;
    using Redpoint.Vfs.Abstractions;
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class GitIndex
    {
        internal readonly Dictionary<string, VfsEntry[]> _directories;
        internal readonly Dictionary<string, string> _files;
        internal readonly Dictionary<string, VfsEntry> _paths;

        const int _cacheVersion = 6;

        internal GitIndex()
        {
            _directories = new Dictionary<string, VfsEntry[]>();
            _files = new Dictionary<string, string>();
            _paths = new Dictionary<string, VfsEntry>();
        }

        internal async Task InitializeFromTreeAsync(IGitTree tree, GitTreeEnumerationMetrics metrics, CancellationToken cancellationToken)
        {
            var directoriesUnsorted = new Dictionary<string, List<VfsEntry>>();
            await foreach (var entry in tree.EnumerateRecursivelyAsync(metrics, cancellationToken))
            {
                if (entry.Name == "." ||
                    entry.Name == "..")
                {
                    continue;
                }

                if (!directoriesUnsorted.TryGetValue(entry.AbsoluteParentPath, out List<VfsEntry>? vfsEntries))
                {
                    vfsEntries = new List<VfsEntry>();
                    directoriesUnsorted.Add(entry.AbsoluteParentPath, vfsEntries);
                }

                if (entry.IsDirectory)
                {
                    _paths[entry.AbsolutePath] = entry;
                    vfsEntries.Add(entry);
                }
                else
                {
                    _paths[entry.AbsolutePath] = entry;
                    vfsEntries.Add(entry);
                    if (entry.BlobSha == null)
                    {
                        throw new InvalidOperationException("BlobSha must be set for files!");
                    }
                    _files.Add(entry.AbsolutePath, entry.BlobSha);
                }
            }
            var comparer = new FileSystemNameComparer();
            foreach (var kv in directoriesUnsorted)
            {
                _directories.Add(kv.Key, kv.Value.OrderBy(x => x.Name, comparer).ToArray());
            }
        }

        internal void WriteTreeToBinaryPath(string path)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteTreeToStream(stream);
            }
        }

        internal void WriteTreeToStream(Stream stream)
        {
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
            {
                writer.Write(_cacheVersion);
                writer.Write(_paths.Count);
                int i = 0;
                var mappings = new Dictionary<VfsEntry, int>();
                foreach (var kv in _paths)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value.Name);
                    writer.Write(kv.Value.Size);
                    writer.Write(kv.Value.IsDirectory);
                    writer.Write(kv.Value.CreationTime.UtcTicks);
                    writer.Write(kv.Value.LastAccessTime.UtcTicks);
                    writer.Write(kv.Value.LastWriteTime.UtcTicks);
                    writer.Write(kv.Value.ChangeTime.UtcTicks);
                    mappings.Add(kv.Value, i);
                    i++;
                }
                writer.Write(_directories.Count);
                foreach (var kv in _directories)
                {
                    writer.Write(kv.Key);
                    writer.Write(kv.Value.Length);
                    foreach (var entry in kv.Value)
                    {
                        // Writes an int that points back to the entry we originally wrote.
                        writer.Write(mappings[entry]);
                    }
                }
                writer.Write(_files.Count);
                foreach (var file in _files)
                {
                    writer.Write(file.Key);
                    // We have to load this in ReadTreeFromBinaryPath since the Blob
                    // object is something inside libgit2.
                    writer.Write(file.Value);
                }
            }
        }

        internal bool ReadTreeFromBinaryPath(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return ReadTreeFromStream(stream);
            }
        }

        internal bool ReadTreeFromStream(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true))
            {
                var version = reader.ReadInt32();
                if (version != _cacheVersion)
                {
                    // Written in an older or newer format.
                    return false;
                }
                var pathCount = reader.ReadInt32();
                var mappings = new Dictionary<int, VfsEntry>();
                for (int i = 0; i < pathCount; i++)
                {
                    var entryPath = reader.ReadString();
                    var entry = new VfsEntry
                    {
                        Name = reader.ReadString(),
                        Size = reader.ReadInt64(),
                        Attributes = reader.ReadBoolean() ? FileAttributes.Directory : FileAttributes.Archive,
                        CreationTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
                        LastAccessTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
                        LastWriteTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero),
                        ChangeTime = new DateTimeOffset(reader.ReadInt64(), TimeSpan.Zero)
                    };
                    mappings.Add(i, entry);
                    _paths.Add(entryPath, entry);
                }
                var directoryCount = reader.ReadInt32();
                for (int i = 0; i < directoryCount; i++)
                {
                    var dirPath = reader.ReadString();
                    var mappingCount = reader.ReadInt32();
                    var directoryEntries = new List<VfsEntry>();
                    for (int v = 0; v < mappingCount; v++)
                    {
                        directoryEntries.Add(mappings[reader.ReadInt32()]);
                    }
                    _directories.Add(dirPath, directoryEntries.ToArray());
                }
                var fileCount = reader.ReadInt32();
                for (int i = 0; i < fileCount; i++)
                {
                    var filePath = reader.ReadString();
                    var blobSha = reader.ReadString();
                    _files.Add(filePath, blobSha);
                }
            }
            return true;
        }
    }
}
