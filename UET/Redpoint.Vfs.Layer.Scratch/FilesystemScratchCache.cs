#define USE_CACHABLE 

namespace Redpoint.Vfs.Layer.Scratch
{
    using BitFaster.Caching;
    using BitFaster.Caching.Lru;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.LocalIo;
    using System.Collections.Generic;
    using System.Linq;

#if !USE_CACHABLE
    internal class FilesystemScratchCache : IWindowsVfsFileCallbacks
    {
        private static IComparer<string> _comparer = new FileSystemNameComparer();

        public FilesystemScratchCache()
        {
        }

        public void OnObjectModifiedAtRelativePath(string relativePath)
        {
        }

        public VfsEntry[] GetProjectionEntriesForScratchPath(string absolutePathOnDisk, string relativePath)
        {
            // @todo: Figure out how to cache this in a thread-safe way! The implementations
            // that are #if'd out below don't work in practice.
            var fsis = new DirectoryInfo(absolutePathOnDisk).GetFileSystemInfos();
            var store = new List<VfsEntry>();
            foreach (var fsi in fsis.OrderBy(x => x.Name, _comparer))
            {
                if (fsi is DirectoryInfo di)
                {
                    store.Add(new VfsEntry
                    {
                        Name = di.Name,
                        CreationTime = di.CreationTime,
                        LastAccessTime = di.LastAccessTime,
                        LastWriteTime = di.LastWriteTime,
                        ChangeTime = di.LastWriteTime,
                        Attributes = di.Attributes,
                        Size = 0,
                    });
                }
                else if (fsi is FileInfo fi)
                {
                    store.Add(new VfsEntry
                    {
                        Name = fi.Name,
                        CreationTime = fi.CreationTime,
                        LastAccessTime = fi.LastAccessTime,
                        LastWriteTime = fi.LastWriteTime,
                        ChangeTime = fi.LastWriteTime,
                        Attributes = fi.Attributes,
                        Size = fi.Length,
                    });
                }
            }
            return store.ToArray();
        }
    }
#else
#if !USE_BREAKABLE
    internal sealed class FilesystemScratchCache : IVfsFileWriteCallbacks
    {
        private ICache<string, VfsEntry[]> _projectionCache;
        private static IComparer<string> _comparer = new FileSystemNameComparer();

        public FilesystemScratchCache()
        {
            _projectionCache = new ConcurrentLruBuilder<string, VfsEntry[]>()
                .WithAtomicGetOrAdd()
                .WithCapacity(2048)
                .WithKeyComparer(new FileSystemNameComparer())
                .Build();
        }

        /// <summary>
        /// This must be called whenever a file or directory is created, modified (written to due
        /// to LastWriteTime) or deleted within the scratch area.
        /// </summary>
        /// <param name="relativePath">
        /// The relative path inside the scratch folder of the file or 
        /// directory being modified (not the path to evict from the cache).
        /// </param>
        public void OnObjectModifiedAtRelativePath(string relativePath)
        {
            var directoryName = Path.GetDirectoryName(relativePath) ?? string.Empty;
            _projectionCache.TryRemove(directoryName);
        }

        public VfsEntry[] GetProjectionEntriesForScratchPath(string absolutePathOnDisk, string relativePath)
        {
            return _projectionCache.GetOrAdd(relativePath, (_) =>
                {
                    var fsis = new DirectoryInfo(absolutePathOnDisk).GetFileSystemInfos();
                    var store = new List<VfsEntry>();
                    foreach (var fsi in fsis.OrderBy(x => x.Name, _comparer))
                    {
                        if (fsi is DirectoryInfo di)
                        {
                            store.Add(new VfsEntry
                            {
                                Name = di.Name,
                                CreationTime = di.CreationTime,
                                LastAccessTime = di.LastAccessTime,
                                LastWriteTime = di.LastWriteTime,
                                ChangeTime = di.LastWriteTime,
                                Attributes = di.Attributes,
                                Size = 0,
                            });
                        }
                        else if (fsi is FileInfo fi)
                        {
                            store.Add(new VfsEntry
                            {
                                Name = fi.Name,
                                CreationTime = fi.CreationTime,
                                LastAccessTime = fi.LastAccessTime,
                                LastWriteTime = fi.LastWriteTime,
                                ChangeTime = fi.LastWriteTime,
                                Attributes = fi.Attributes,
                                Size = fi.Length,
                            });
                        }
                    }
                    return store.ToArray();
                });
        }
    }
#else
    internal class FilesystemScratchCache : IWindowsVfsFileCallbacks
    {
        private ICache<string, BreakableEntry<string, VfsEntry[]>> _projectionCache;
        private static IComparer<string> _comparer = new FileSystemNameComparer();

        public FilesystemScratchCache()
        {
            _projectionCache = new ConcurrentLruBuilder<string, BreakableEntry<string, VfsEntry[]>>()
                .WithAtomicGetOrAdd()
                .WithCapacity(2048)
                .WithKeyComparer(new FileSystemNameComparer())
                .Build();
        }

        private class BreakableEntry<K, V>
        {
            private ReaderWriterLockSlim _rwLock = new ReaderWriterLockSlim();
            private V? _cached;
            private volatile bool _didBreakMidflight;
            private readonly K _key;
            private readonly Func<K, V> _factory;

            public BreakableEntry(K key, Func<K, V> factory)
            {
                _key = key;
                _factory = factory;
            }

            public void Break()
            {
                _didBreakMidflight = true;
            }

            public V Entries
            {
                get
                {
                    return _factory(_key);
                }
            }

            /*
            public V Entries
            {
                get
                {
                    _rwLock.EnterUpgradeableReadLock();
                    try
                    {
                        if (_cached != null && !_didBreakMidflight)
                        {
                            return _cached;
                        }
                        _rwLock.EnterWriteLock();
                        try
                        {
                            if (_cached != null && !_didBreakMidflight)
                            {
                                return _cached;
                            }
                            var entries = _factory(_key);
                            if (!_didBreakMidflight)
                            {
                                _cached = entries;
                            }
                            else
                            {
                                _didBreakMidflight = false;
                            }
                            return entries;
                        }
                        finally
                        {
                            _rwLock.ExitWriteLock();
                        }
                    }
                    finally
                    {
                        _rwLock.ExitUpgradeableReadLock();
                    }
                }
            }
            */
        }

        /// <summary>
        /// This must be called whenever a file or directory is created, modified (written to due
        /// to LastWriteTime) or deleted within the scratch area.
        /// </summary>
        /// <param name="relativePath">
        /// The relative path inside the scratch folder of the file or 
        /// directory being modified (not the path to evict from the cache).
        /// </param>
        public void OnObjectModifiedAtRelativePath(string relativePath)
        {
            var directoryName = Path.GetDirectoryName(relativePath) ?? string.Empty;
            if (_projectionCache.TryGet(directoryName, out var cache))
            {
                cache.Break();
            }
        }

        public VfsEntry[] GetProjectionEntriesForScratchPath(string absolutePathOnDisk, string relativePath)
        {
            return _projectionCache.GetOrAdd(relativePath, (_) => new BreakableEntry<string, VfsEntry[]>(
                absolutePathOnDisk,
                (absolutePathOnDisk) =>
                {
                    var fsis = new DirectoryInfo(absolutePathOnDisk).GetFileSystemInfos();
                    var store = new List<VfsEntry>();
                    foreach (var fsi in fsis.OrderBy(x => x.Name, _comparer))
                    {
                        if (fsi is DirectoryInfo di)
                        {
                            store.Add(new VfsEntry
                            {
                                Name = di.Name,
                                CreationTime = di.CreationTime,
                                LastAccessTime = di.LastAccessTime,
                                LastWriteTime = di.LastWriteTime,
                                ChangeTime = di.LastWriteTime,
                                Attributes = di.Attributes,
                                Size = 0,
                            });
                        }
                        else if (fsi is FileInfo fi)
                        {
                            store.Add(new VfsEntry
                            {
                                Name = fi.Name,
                                CreationTime = fi.CreationTime,
                                LastAccessTime = fi.LastAccessTime,
                                LastWriteTime = fi.LastWriteTime,
                                ChangeTime = fi.LastWriteTime,
                                Attributes = fi.Attributes,
                                Size = fi.Length,
                            });
                        }
                    }
                    return store.ToArray();
                })).Entries;
        }
    }
#endif
#endif
}
