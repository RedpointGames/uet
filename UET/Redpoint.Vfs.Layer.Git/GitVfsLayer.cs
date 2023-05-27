namespace Redpoint.Vfs.Layer.Git
{
    using BitFaster.Caching.Lru;
    using Microsoft.Extensions.Logging;
    using Redpoint.Git.Abstractions;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.LocalIo;
    using System.Diagnostics;
    using System.IO;
    using static Redpoint.Git.Abstractions.IGitTree;

    internal class GitVfsLayer : IGitVfsLayer
    {
        private readonly IGitRepository _repository;
        private readonly string _commitHash;
        private readonly ConcurrentLru<string, bool> _materializationCache;
        private readonly ILogger _logger;
        private readonly ILocalIoVfsFileFactory _localIoVfsFileFactory;
        private readonly string _blobPath;
        private readonly string _indexCachePath;
        private bool _didInit;
        private readonly SemaphoreSlim _globalSemaphore;
        private IGitCommit? _commit;
        private DateTimeOffset _created;
        private readonly GitIndex _index;

        internal GitVfsLayer(
            ILogger<GitVfsLayer> logger,
            ILocalIoVfsFileFactory localIoVfsFileFactory,
            IGitRepository repository,
            string blobPath,
            string indexCachePath,
            string commitHash)
        {
            _blobPath = blobPath;
            if (!Directory.Exists(_blobPath))
            {
                Directory.CreateDirectory(_blobPath);
            }
            _indexCachePath = indexCachePath;
            if (!Directory.Exists(_indexCachePath))
            {
                Directory.CreateDirectory(_indexCachePath);
            }
            _repository = repository;
            _index = new GitIndex();
            _commitHash = commitHash;
            _didInit = false;
            _logger = logger;
            _localIoVfsFileFactory = localIoVfsFileFactory;
            _materializationCache = new ConcurrentLru<string, bool>(1024 * 32);
            _globalSemaphore = new SemaphoreSlim(1);
        }

        public IReadOnlyDictionary<string, string> Files => _index._files;

        public DateTimeOffset Created => _created;

        public async Task InitAsync(CancellationToken cancellationToken)
        {
            if (!_didInit)
            {
                _didInit = true;

                var sha = await _repository.ResolveRefToShaAsync(_commitHash, cancellationToken);
                _commit = await _repository.GetCommitByShaAsync(sha!, cancellationToken);
                _created = _commit.CommittedAtUtc;

                var tree = await _commit.GetRootTreeAsync(cancellationToken);

                var cachedIndexTreePath = Path.Combine(_indexCachePath, tree.Sha);

                if (File.Exists(cachedIndexTreePath))
                {
                    if (_index.ReadTreeFromBinaryPath(cachedIndexTreePath))
                    {
                        Console.WriteLine($"Git parsed objects (from index cache)");
                        return;
                    }
                }

                var metrics = new GitTreeEnumerationMetrics(true);
                await _index.InitializeFromTreeAsync(tree, metrics, cancellationToken);

                try
                {
                    _index.WriteTreeToBinaryPath(cachedIndexTreePath);
                    Console.WriteLine($"Git parsed objects (wrote to index cache)");
                }
                catch
                {
                    Console.WriteLine($"Git parsed objects (without writing to cache)");
                }
            }
        }

        public bool ReadOnly => true;

        public void Dispose()
        {
            _repository.Dispose();
        }

        public IEnumerable<VfsEntry>? List(string path)
        {
            if (!_didInit)
            {
                throw new InvalidOperationException("Git layer must be asynchronously initialized first!");
            }
            var gitPath = path.TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
            if (!_index._directories.ContainsKey(gitPath))
            {
                return null;
            }
            if (_index._directories[gitPath].Any(x => x.Name == "."))
            {
                throw new InvalidOperationException("Git data is corrupt!");
            }
            return _index._directories[gitPath];
        }

        public VfsEntryExistence Exists(string path)
        {
            var gitPath = path.TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();

            if (_index._directories.ContainsKey(gitPath))
            {
                return VfsEntryExistence.DirectoryExists;
            }
            else if (_index._files.ContainsKey(gitPath))
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
            var gitPath = path.TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
            if (_index._paths.ContainsKey(gitPath))
            {
                return _index._paths[gitPath];
            }
            else
            {
                return null;
            }
        }

        private string AdjustNewLinesForWindows(string content)
        {
            if (!content.Contains("\r"))
            {
                return content.Replace("\n", "\r\n");
            }
            return content;
        }

        public IVfsFileHandle<IVfsFile>? OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, ref VfsEntry? metadata)
        {
            bool acquiredGlobalSemaphore = false;
            if (Environment.GetEnvironmentVariable("UEFS_GIT_TEST_LOCK_MODE") == "global-semaphore")
            {
                _globalSemaphore.Wait();
                acquiredGlobalSemaphore = true;
            }

            try
            {
                if (!_didInit)
                {
                    throw new InvalidOperationException("Git layer must be asynchronously initialized first!");
                }
                if (!fileMode.IsReadOnlyAccess(fileAccess))
                {
                    throw new InvalidOperationException();
                }
                var gitPath = path.TrimStart(Path.DirectorySeparatorChar).ToLowerInvariant();
                if (!_index._files.ContainsKey(gitPath))
                {
                    return null;
                }

                // The LibGit2 streams are extremely slow for repeated and large file access. As soon as we're
                // accessing a file, expand it completely and then use a stream that is just an on-disk
                // version from then on out.
                var expandedPath = Path.Combine(_blobPath, _index._files[gitPath]);
                // Force this to run on a background thread, as we only have a synchronous context here (we're
                // being called from a VFS, so proper async is impossible). Then we call Task.WaitAll
                // to block the VFS thread until we get a result.
                var bgTask = Task.Run(async () => await _materializationCache.GetOrAddAsync(
                    expandedPath,
                    async expandedPath =>
                    {
                        // Ensure each file is only materialized once and never concurrently.
                        using (KeyedSemaphores.KeyedSemaphore.Lock($"git-materialization:{expandedPath.ToLowerInvariant()}"))
                        {
                            if (!File.Exists(expandedPath))
                            {
                                Func<string, string>? contentAdjust = null;
                                if (gitPath.EndsWith(".bat", StringComparison.InvariantCultureIgnoreCase) ||
                                    gitPath.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // These files can be stored in Git with UNIX line endings, but
                                    // they don't work at all on Windows if they don't use \r\n, so
                                    // transparently adjust them.
                                    contentAdjust = AdjustNewLinesForWindows;
                                }

                                var stopwatch = Stopwatch.StartNew();
                                var fileSha = _index._files[gitPath];
                                var extractedSize = await _repository.MaterializeBlobToDiskByShaAsync(fileSha, expandedPath, contentAdjust, CancellationToken.None);
#if ENABLE_TRACE_LOGS
                                _logger.LogTrace($"Materialized {gitPath} to disk ({extractedSize} bytes in {stopwatch.ElapsedMilliseconds.ToString("F2")} ms).");
#endif
                            }
                        }
                        return true;
                    }));
                bgTask.Wait();
                if (bgTask.IsFaulted)
                {
                    _ = bgTask.Result;
                }
                if (!bgTask.IsCompleted)
                {
                    throw new InvalidOperationException("Background task did not complete, even though we called Wait!");
                }
                var fileInfo = new FileInfo(expandedPath);
                metadata = new VfsEntry
                {
                    Name = fileInfo.Name,
                    CreationTime = _created,
                    LastAccessTime = _created,
                    LastWriteTime = _created,
                    ChangeTime = _created,
                    Attributes = FileAttributes.Archive,
                    Size = fileInfo.Length,
                };
                return _localIoVfsFileFactory.CreateVfsFileHandle(expandedPath, FileMode.Open, FileAccess.Read, FileShare.Read, null, null);
            }
            finally
            {
                if (acquiredGlobalSemaphore)
                {
                    _globalSemaphore.Release();
                }
            }
        }

        public bool CreateDirectory(string path)
        {
            throw new NotSupportedException("GitProjectionLayer CreateDirectory");
        }

        public bool MoveFile(string oldPath, string newPath, bool replace)
        {
            throw new NotSupportedException("GitProjectionLayer MoveFile");
        }

        public bool DeleteFile(string path)
        {
            throw new NotSupportedException("GitProjectionLayer DeleteFile");
        }

        public bool DeleteDirectory(string path)
        {
            throw new NotSupportedException("GitProjectionLayer DeleteDirectory");
        }

        public bool SetBasicInfo(string path, uint? attributes, DateTimeOffset? creationTime, DateTimeOffset? lastAccessTime, DateTimeOffset? lastWriteTime, DateTimeOffset? changeTime)
        {
            throw new NotSupportedException("GitProjectionLayer SetBasicInfo");
        }
    }
}
