namespace Redpoint.Vfs.Layer.Folder
{
    using BitFaster.Caching.Lru;
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.LocalIo;
    using System.Collections.Concurrent;
    using System.Runtime.Versioning;


    internal class FolderVfsLayer : IVfsLayer
    {
        private readonly ILogger<FolderVfsLayer> _logger;
        private readonly ILocalIoVfsFileFactory _localIoVfsFileFactory;
        private readonly string _path;
        private readonly IVfsLayer? _nextLayer;
        private readonly ConcurrentLru<string, VfsEntry[]?> _vfsEntryCache;
        private readonly ConcurrentDictionary<string, bool> _fileExistsCache;
        private readonly ConcurrentDictionary<string, bool> _directoryExistsCache;
        private static IComparer<string> _comparer = new FileSystemNameComparer();

        public FolderVfsLayer(
            ILogger<FolderVfsLayer> logger,
            ILocalIoVfsFileFactory localIoVfsFileFactory,
            string path,
            IVfsLayer? nextLayer)
        {
            _logger = logger;
            _localIoVfsFileFactory = localIoVfsFileFactory;
            _path = path;
            _nextLayer = nextLayer;
            _vfsEntryCache = new ConcurrentLru<string, VfsEntry[]?>(2048);
            _fileExistsCache = new ConcurrentDictionary<string, bool>();
            _directoryExistsCache = new ConcurrentDictionary<string, bool>();
        }

        public void Dispose()
        {
            _nextLayer?.Dispose();
        }

        public bool ReadOnly => true;

        private IEnumerable<VfsEntry> ListAggregated(DirectoryInfo di, string path)
        {
            // @todo: Cache this.
            var entries = di.GetFileSystemInfos().OrderBy(x => x.Name, _comparer).Select(fsi =>
            {
                if (fsi is DirectoryInfo di)
                {
                    return new VfsEntry
                    {
                        Name = di.Name,
                        CreationTime = di.CreationTime,
                        LastAccessTime = di.LastAccessTime,
                        LastWriteTime = di.LastWriteTime,
                        ChangeTime = di.LastWriteTime,
                        Attributes = di.Attributes,
                        Size = 0,
                    };
                }
                else if (fsi is FileInfo fi)
                {
                    return new VfsEntry
                    {
                        Name = fi.Name,
                        CreationTime = fi.CreationTime,
                        LastAccessTime = fi.LastAccessTime,
                        LastWriteTime = fi.LastWriteTime,
                        ChangeTime = fi.LastWriteTime,
                        Attributes = fi.Attributes,
                        Size = fi.Length,
                    };
                }
                return null;
            }).Where(x => x != null).OfType<VfsEntry>();

            return DirectoryAggregation.Aggregate(
                _nextLayer?.List(path),
                entries);
        }

        public IEnumerable<VfsEntry>? List(string path)
        {
            var targetPath = Path.Combine(_path, path.TrimStart(new[] { '\\', '/' }));
            return _vfsEntryCache.GetOrAdd(targetPath, targetPath =>
            {
                var di = new DirectoryInfo(targetPath);
                if (di.Exists)
                {
                    return ListAggregated(di, path).ToArray();
                }
                else
                {
                    // Otherwise this layer isn't responsible for this path at all, delegate
                    // entirely to the next layer down.
                    return _nextLayer?.List(path)?.ToArray();
                }
            });
        }

        private bool DirectoryExists(string path)
        {
            return _directoryExistsCache.GetOrAdd(path, path =>
            {
                var di = new DirectoryInfo(Path.Combine(_path, path.TrimStart(new[] { '\\', '/' })));
                return di.Exists || (_nextLayer?.Exists(path) == VfsEntryExistence.DirectoryExists);
            });
        }

        private bool FileExists(string path)
        {
            return _fileExistsCache.GetOrAdd(path, path =>
            {
                var fi = new FileInfo(Path.Combine(_path, path.TrimStart(new[] { '\\', '/' })));
                return fi.Exists || (_nextLayer?.Exists(path) == VfsEntryExistence.FileExists);
            });
        }

        public VfsEntryExistence Exists(string path)
        {
            if (DirectoryExists(path))
            {
                return VfsEntryExistence.DirectoryExists;
            }
            else if (FileExists(path))
            {
                return VfsEntryExistence.FileExists;
            }
            else
            {
                return VfsEntryExistence.DoesNotExist;
            }
        }

        public VfsEntry? GetInfo(string path)
        {
            var fi = new FileInfo(Path.Combine(_path, path.TrimStart(new[] { '\\', '/' })));
            var di = new DirectoryInfo(Path.Combine(_path, path.TrimStart(new[] { '\\', '/' })));
            if (fi.Exists)
            {
                return new VfsEntry
                {
                    Name = fi.Name,
                    CreationTime = fi.CreationTime,
                    LastAccessTime = fi.LastAccessTime,
                    LastWriteTime = fi.LastWriteTime,
                    ChangeTime = fi.LastWriteTime,
                    Attributes = fi.Attributes,
                    Size = fi.Length,
                };
            }
            else if (di.Exists)
            {
                return new VfsEntry
                {
                    Name = di.Name,
                    CreationTime = di.CreationTime,
                    LastAccessTime = di.LastAccessTime,
                    LastWriteTime = di.LastWriteTime,
                    ChangeTime = di.LastWriteTime,
                    Attributes = di.Attributes,
                    Size = 0,
                };
            }
            else
            {
                return _nextLayer?.GetInfo(path);
            }
        }

        public IVfsFileHandle<IVfsFile>? OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, ref VfsEntry? metadata)
        {
            if (!fileMode.IsReadOnlyAccess(fileAccess))
            {
                throw new NotSupportedException("FolderProjectionLayer OpenFile !IsReadOnlyAccess");
            }
            var fi = new FileInfo(Path.Combine(_path, path.TrimStart(new[] { '\\', '/' })));
            if (fi.Exists)
            {
                metadata = new VfsEntry
                {
                    Name = fi.Name,
                    CreationTime = fi.CreationTime,
                    LastAccessTime = fi.LastAccessTime,
                    LastWriteTime = fi.LastWriteTime,
                    ChangeTime = fi.LastWriteTime,
                    Attributes = fi.Attributes,
                    Size = fi.Length,
                };
                return _localIoVfsFileFactory.CreateVfsFileHandle(
                    fi.FullName,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    null,
                    null);
            }
            else
            {
                return _nextLayer?.OpenFile(path, fileMode, fileAccess, fileShare, ref metadata);
            }
        }

        public bool CreateDirectory(string path)
        {
            throw new NotSupportedException("FolderProjectionLayer CreateDirectory");
        }

        public bool MoveFile(string oldPath, string newPath, bool replace)
        {
            throw new NotSupportedException("FolderProjectionLayer MoveFile");
        }

        public bool DeleteFile(string path)
        {
            throw new NotSupportedException("FolderProjectionLayer DeleteFile");
        }

        public bool DeleteDirectory(string path)
        {
            throw new NotSupportedException("FolderProjectionLayer DeleteDirectory");
        }

        public bool SetBasicInfo(string path, uint? attributes, DateTimeOffset? creationTime, DateTimeOffset? lastAccessTime, DateTimeOffset? lastWriteTime, DateTimeOffset? changeTime)
        {
            throw new NotSupportedException("FolderProjectionLayer SetBasicInfo");
        }
    }
}