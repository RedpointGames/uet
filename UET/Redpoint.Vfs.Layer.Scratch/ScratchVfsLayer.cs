namespace Redpoint.Vfs.Layer.Scratch
{
    using BitFaster.Caching.Lru;
    using KeyedSemaphores;
    using Microsoft.Extensions.Logging;
    using System.Collections.Generic;
    using System.IO;
    using FileMode = System.IO.FileMode;
    using FileAccess = System.IO.FileAccess;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.LocalIo;
    using System.Collections.Concurrent;

    internal sealed class ScratchVfsLayer : IScratchVfsLayer
    {
        private readonly ILogger<ScratchVfsLayer> _logger;
        private readonly ILocalIoVfsFileFactory _localIoVfsFileFactory;
        private readonly string _scratchPath;
        private readonly IVfsLayer? _nextLayer;
        private readonly IVfsLocks _openAndReadLock;
        private readonly IVfsLocks _materializationAndTombstoningLock;
        private readonly ConcurrentLru<string, (ScratchVfsPathStatus status, VfsEntryExistence existence)> _pathStatusCache;
        private readonly FilesystemScratchIndex _fsScratchIndex;
        private readonly FilesystemScratchCache _fsScratchCache;
        private readonly bool _enableCorrectnessChecks;
        private static readonly TimeSpan _deadlockThreshold = TimeSpan.FromSeconds(10);

        public ScratchVfsLayer(
            ILogger<ScratchVfsLayer> logger,
            ILogger<FilesystemScratchIndex> fileSystemScratchLogger,
            ILocalIoVfsFileFactory localIoVfsFileFactory,
            string path,
            IVfsLayer? nextLayer,
            bool enableCorrectnessChecks = false)
        {
            _logger = logger;
            _localIoVfsFileFactory = localIoVfsFileFactory;
            _scratchPath = path;
            _nextLayer = nextLayer;
            _openAndReadLock = new SemaphoreSlimVfsLocks();
            _materializationAndTombstoningLock = new SemaphoreSlimVfsLocks();
            _pathStatusCache = new ConcurrentLru<string, (ScratchVfsPathStatus, VfsEntryExistence)>(
                Environment.ProcessorCount,
                16 * 1024,
                new FileSystemNameComparer());
            _fsScratchCache = new FilesystemScratchCache();
            _fsScratchIndex = new FilesystemScratchIndex(
                fileSystemScratchLogger,
                _fsScratchCache,
                Path.Combine(path, ".uefs.db"),
                path);
            _enableCorrectnessChecks = enableCorrectnessChecks;

            Directory.CreateDirectory(_scratchPath);
        }

        private string NormalizePathKey(string path)
        {
            return path.ToLowerInvariant();
        }

        public bool ReadOnly => false;

        private string GetScratchPath(string path)
        {
            return Path.Combine(_scratchPath, path.TrimStart(new[] { '\\', '/' }));
        }

        private void CreateScratchTombstone(string path)
        {
            _fsScratchIndex.SetScratchIndex(path, FilesystemScratchIndex.Status_Tombstoned);
        }

        private void DeleteScratchTombstone(string path)
        {
            _fsScratchIndex.ClearScratchIndex(path);
        }

        public (ScratchVfsPathStatus status, VfsEntryExistence existence) GetPathStatus(string path)
        {
            return _pathStatusCache.GetOrAdd(
                path,
                path =>
                {
                    var result = GetPathStatusInternal(path);
#if ENABLE_TRACE_LOGS
                    _logger.LogTrace($"Loaded into path cache: {path} = {result}");
#endif
                    return result;
                });
        }

        private (ScratchVfsPathStatus status, VfsEntryExistence existence) GetPathStatusInternal(string path)
        {
            var scratchValue = _fsScratchIndex.GetScratchIndex(path);
            if (scratchValue == FilesystemScratchIndex.Status_ExistsFile)
            {
                return (ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists);
            }
            else if (scratchValue == FilesystemScratchIndex.Status_ExistsDir)
            {
                return (ScratchVfsPathStatus.Materialized, VfsEntryExistence.DirectoryExists);
            }
            else if (scratchValue == FilesystemScratchIndex.Status_Tombstoned)
            {
                return (ScratchVfsPathStatus.NonexistentTombstoned, VfsEntryExistence.DoesNotExist);
            }
            else
            {
                if (_nextLayer != null)
                {
                    var type = _nextLayer.Exists(path);
                    if (type == VfsEntryExistence.DoesNotExist)
                    {
                        return (ScratchVfsPathStatus.Nonexistent, VfsEntryExistence.DoesNotExist);
                    }
                    else
                    {
                        return (ScratchVfsPathStatus.Passthrough, type);
                    }
                }
                else
                {
                    return (ScratchVfsPathStatus.Nonexistent, VfsEntryExistence.DoesNotExist);
                }
            }
        }

        private void ApplyDeletionAndTombstoningToPath(string path)
        {
            if (!_materializationAndTombstoningLock.TryLock("ApplyDeletionAndTombstoningToPath", NormalizePathKey(path), _deadlockThreshold, () =>
            {
                ApplyDeletionAndTombstoningToPathInternal(path);
            }, out var blockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{blockingContext}' while trying to obtain MaterializationAndTombstoningLock for ApplyDeletionAndTombstoningToPath({path})!");
            }
        }

        private void ApplyDeletionAndTombstoningToPathInternal(string path)
        {
            var scratchPath = GetScratchPath(path);
            var isTombstoned = false;

            var scratchStatus = _fsScratchIndex.GetScratchIndex(path);

            if (scratchStatus == FilesystemScratchIndex.Status_ExistsFile)
            {
                File.Delete(scratchPath);
                _fsScratchIndex.ClearScratchIndex(path);
            }
            else if (scratchStatus == FilesystemScratchIndex.Status_ExistsDir)
            {
                Directory.Delete(scratchPath, true);
                _fsScratchIndex.ClearScratchIndexRecursive(path);
            }

            if (_nextLayer != null)
            {
                if (_nextLayer.Exists(path) != VfsEntryExistence.DoesNotExist)
                {
                    CreateScratchTombstone(path);
                    isTombstoned = true;
                }
            }

            SetPathStatusDirect(path, (isTombstoned ? ScratchVfsPathStatus.NonexistentTombstoned : ScratchVfsPathStatus.Nonexistent, VfsEntryExistence.DoesNotExist));
        }

        private void ApplyTombstoningToPath(string path)
        {
            if (!_materializationAndTombstoningLock.TryLock("ApplyTombstoningToPath", NormalizePathKey(path), _deadlockThreshold, () =>
            {
                ApplyTombstoningToPathInternal(path);
            }, out var blockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{blockingContext}' while trying to obtain MaterializationAndTombstoningLock for ApplyTombstoningToPath({path})!");
            }
        }

        private void ApplyTombstoningToPathInternal(string path)
        {
            // If the previous status is Passthrough, we can call ApplyTombstoningToPath instead of the more
            // complex ApplyDeletionAndTombstoningToPath when deleting a passthrough file.
            CreateScratchTombstone(path);
            SetPathStatusDirect(path, (ScratchVfsPathStatus.NonexistentTombstoned, VfsEntryExistence.DoesNotExist));
        }

        private void ApplyMaterializationToPath(string path)
        {
            if (!_materializationAndTombstoningLock.TryLock("ApplyMaterializationToPath", NormalizePathKey(path), _deadlockThreshold, () =>
            {
                ApplyMaterializationToPathInternal(path);
            }, out var blockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{blockingContext}' while trying to obtain MaterializationAndTombstoningLock for ApplyMaterializationToPath({path})!");
            }
        }

        private void CreateScratchDirectory(string? path)
        {
            var pathBuilding = string.Empty;
            var casePreserveBuilding = string.Empty;
            var nestedLayerPathBreak = false;
            var prefix = string.Empty;
            foreach (var component in (path ?? string.Empty).Split(Path.DirectorySeparatorChar))
            {
                var normalizedComponent = NormalizePathKey(component);
                pathBuilding += prefix + normalizedComponent;
                _fsScratchIndex.SetScratchIndex(pathBuilding, FilesystemScratchIndex.Status_ExistsDir);

                if (!nestedLayerPathBreak)
                {
                    var existingDirectory = _nextLayer?.GetInfo(pathBuilding);
                    if (existingDirectory != null)
                    {
                        casePreserveBuilding += prefix + existingDirectory.Name;
                    }
                    else
                    {
                        casePreserveBuilding += prefix + component;
                        nestedLayerPathBreak = true;
                    }
                }
                else
                {
                    casePreserveBuilding += prefix + component;
                }

                Directory.CreateDirectory(GetScratchPath(casePreserveBuilding));
                SetPathStatusDirect(casePreserveBuilding, (ScratchVfsPathStatus.Materialized, VfsEntryExistence.DirectoryExists));

                prefix = "\\";
            }
        }

        private void ApplyMaterializationToPathInternal(string path)
        {
            var layerExistance = _nextLayer!.Exists(path);
            if (layerExistance == VfsEntryExistence.DirectoryExists)
            {
                CreateScratchDirectory(path);
            }
            else if (layerExistance == VfsEntryExistence.FileExists)
            {
                CreateScratchDirectory(Path.GetDirectoryName(path));
                var scratchPath = GetScratchPath(path);
                VfsEntry? metadata = null;
                using (var readHandle = _nextLayer?.OpenFile(path, FileMode.Open, FileAccess.Read, FileShare.Read, ref metadata))
                {
                    using (var stream = new FileStream(scratchPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        _fsScratchIndex.SetScratchIndex(path, FilesystemScratchIndex.Status_ExistsFile);

                        byte[] buffer = new byte[128 * 1024];
                        for (long offset = 0; offset < readHandle!.VfsFile.Length;)
                        {
                            readHandle.VfsFile.ReadFile(buffer, out uint bytesRead, offset);
                            offset += bytesRead;
                            stream.Write(buffer, 0, (int)bytesRead);
                        }
                    }

                    if (metadata != null)
                    {
                        File.SetCreationTimeUtc(scratchPath, metadata.CreationTime.UtcDateTime);
                        File.SetLastAccessTimeUtc(scratchPath, metadata.LastAccessTime.UtcDateTime);
                        File.SetLastWriteTimeUtc(scratchPath, metadata.LastWriteTime.UtcDateTime);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException("Unable to materialize path that does not exist on a lower layer!");
            }

            SetPathStatusDirect(path, (ScratchVfsPathStatus.Materialized, layerExistance));
        }

        private void SetPathStatusDirect(string path, (ScratchVfsPathStatus status, VfsEntryExistence existence) value)
        {
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Updating path status: {path} => {value}");
#endif
            _pathStatusCache.AddOrUpdate(path, value);
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Updated  path status: {path} = {value}");
#endif
        }

        private void SetPathStatus(string path, ScratchVfsPathStatus status, VfsEntryExistence newType, bool materializeAsTruncated = false)
        {
            var currentStatus = GetPathStatus(path);
            switch (currentStatus.status)
            {
                case ScratchVfsPathStatus.Materialized:
                    switch (status)
                    {
                        case ScratchVfsPathStatus.Materialized:
                            break;
                        case ScratchVfsPathStatus.Passthrough:
                            throw new InvalidOperationException("Can not move from Materialized to Passthrough status.");
                        case ScratchVfsPathStatus.Nonexistent:
                        case ScratchVfsPathStatus.NonexistentTombstoned:
                            ApplyDeletionAndTombstoningToPath(path);
                            break;
                    }
                    break;
                case ScratchVfsPathStatus.Passthrough:
                    switch (status)
                    {
                        case ScratchVfsPathStatus.Materialized:
                            CreateScratchDirectory(Path.GetDirectoryName(path));
                            if (materializeAsTruncated)
                            {
                                File.WriteAllText(GetScratchPath(path), string.Empty);
                                _fsScratchIndex.SetScratchIndex(path, FilesystemScratchIndex.Status_ExistsFile);
                            }
                            else
                            {
                                ApplyMaterializationToPath(path);
                            }
                            break;
                        case ScratchVfsPathStatus.Passthrough:
                            break;
                        case ScratchVfsPathStatus.Nonexistent:
                        case ScratchVfsPathStatus.NonexistentTombstoned:
                            ApplyTombstoningToPath(path);
                            break;
                    }
                    break;
                case ScratchVfsPathStatus.Nonexistent:
                    switch (status)
                    {
                        case ScratchVfsPathStatus.Materialized:
                            // We just trust the caller is about to write a file or directory into this location.
                            _fsScratchIndex.SetScratchIndex(path, newType == VfsEntryExistence.FileExists ? FilesystemScratchIndex.Status_ExistsFile : FilesystemScratchIndex.Status_ExistsDir);
                            SetPathStatusDirect(path, (status, newType));
                            break;
                        case ScratchVfsPathStatus.Passthrough:
                            break;
                        case ScratchVfsPathStatus.Nonexistent:
                        case ScratchVfsPathStatus.NonexistentTombstoned:
                            break;
                    }
                    break;
                case ScratchVfsPathStatus.NonexistentTombstoned:
                    switch (status)
                    {
                        case ScratchVfsPathStatus.Materialized:
                            // We just trust the caller is about to write a file or directory into this location.
                            DeleteScratchTombstone(path);
                            _fsScratchIndex.SetScratchIndex(path, newType == VfsEntryExistence.FileExists ? FilesystemScratchIndex.Status_ExistsFile : FilesystemScratchIndex.Status_ExistsDir);
                            SetPathStatusDirect(path, (status, newType));
                            break;
                        case ScratchVfsPathStatus.Passthrough:
                            break;
                        case ScratchVfsPathStatus.Nonexistent:
                        case ScratchVfsPathStatus.NonexistentTombstoned:
                            break;
                    }
                    break;
            }
        }

        public bool CreateDirectory(string path)
        {
            SetPathStatus(path, ScratchVfsPathStatus.Materialized, VfsEntryExistence.DirectoryExists);
            CreateScratchDirectory(path);
            return true;
        }

        public bool DeleteDirectory(string path)
        {
            SetPathStatus(path, ScratchVfsPathStatus.Nonexistent, VfsEntryExistence.DoesNotExist);
            return true;
        }

        public bool DeleteFile(string path)
        {
            SetPathStatus(path, ScratchVfsPathStatus.Nonexistent, VfsEntryExistence.DoesNotExist);
            return true;
        }

        public VfsEntryExistence Exists(string path)
        {
            var status = GetPathStatus(path);
            return status.existence;
        }

        public void Dispose()
        {
            _fsScratchIndex.Dispose();
            _nextLayer?.Dispose();
        }

        private IEnumerable<VfsEntry> ListAggregated(string path)
        {
            IEnumerable<VfsEntry> result = null!;
            if (!_openAndReadLock.TryLock("ListAggregated", NormalizePathKey(path), TimeSpan.MaxValue, () =>
            {
                var upstream = _nextLayer?.List(path);
                if (upstream != null)
                {
                    // Exclude tombstoned items from the upstream.
                    upstream = upstream.Where(x => GetPathStatus(Path.Combine(path, x.Name)).status != ScratchVfsPathStatus.NonexistentTombstoned);
                }

                result = DirectoryAggregation.Aggregate(
                    upstream,
                    _fsScratchCache.GetProjectionEntriesForScratchPath(GetScratchPath(path), path),
                    _enableCorrectnessChecks);
            }, out var blockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{blockingContext}' while trying to obtain OpenAndReadLock on path for ListAggregated({path})!");
            }
            else
            {
                return result;
            }
        }

        public IEnumerable<VfsEntry>? List(string path)
        {
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Enumerating directory: {path}");
#endif

            var status = GetPathStatus(path);
            IEnumerable<VfsEntry>? enumerable;
            if (status.status == ScratchVfsPathStatus.Materialized)
            {
                enumerable = ListAggregated(path);
            }
            else
            {
                enumerable = _nextLayer?.List(path);
            }

            if (_enableCorrectnessChecks)
            {
                if (enumerable != null)
                {
                    var list = enumerable.ToList();
                    var listNames = list.Select(x => x.Name.ToLowerInvariant()).ToList();
                    if (new HashSet<string>(listNames).Count != listNames.Count)
                    {
                        throw new CorrectnessCheckFailureException("List operation returned duplicated entries!");
                    }
                    return list;
                }
            }

            return enumerable;
        }

        public bool MoveFile(string oldPath, string newPath, bool replace)
        {
            bool result = false;
            if (!_openAndReadLock.TryLock("MoveFile:Old", NormalizePathKey(oldPath), _deadlockThreshold, () =>
            {
                if (!_openAndReadLock.TryLock("MoveFile:New", NormalizePathKey(newPath), _deadlockThreshold, () =>
                {
                    result = MoveFileInternal(oldPath, newPath, replace);
                }, out var newBlockingContext))
                {
                    throw new VfsLayerDeadlockException($"Blocked by '{newBlockingContext}' while trying to obtain OpenAndReadLock on new path for MoveFile({oldPath}, {newPath}, {replace})!");
                }
            }, out var oldBlockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{oldBlockingContext}' while trying to obtain OpenAndReadLock on old path for MoveFile({oldPath}, {newPath}, {replace})!");
            }
            return result;
        }

        private bool MoveFileInternal(string oldPath, string newPath, bool replace)
        {
            var (oldStatus, oldType) = GetPathStatus(oldPath);
            if (oldType == VfsEntryExistence.DoesNotExist)
            {
                return false;
            }

            var (newStatus, newType) = GetPathStatus(newPath);
            if (newType != VfsEntryExistence.DoesNotExist)
            {
                if (!replace)
                {
                    return false;
                }
                else if (newType == VfsEntryExistence.FileExists)
                {
                    DeleteFile(newPath);
                }
                else if (newType == VfsEntryExistence.DirectoryExists)
                {
                    DeleteDirectory(newPath);
                }
            }

            if (newStatus == ScratchVfsPathStatus.NonexistentTombstoned)
            {
                DeleteScratchTombstone(newPath);
            }

            if (oldStatus == ScratchVfsPathStatus.Materialized)
            {
                // We can do a fast move entirely within the scratch layer.
                var oldScratchPath = GetScratchPath(oldPath);
                if (oldType == VfsEntryExistence.DirectoryExists)
                {
                    Directory.Move(oldScratchPath, GetScratchPath(newPath));
                    _fsScratchIndex.MoveScratchIndexRecursive(oldPath, newPath);
                }
                else
                {
                    File.Move(oldScratchPath, GetScratchPath(newPath));
                    _fsScratchIndex.ClearScratchIndex(oldPath);
                    _fsScratchIndex.SetScratchIndex(newPath, FilesystemScratchIndex.Status_ExistsFile);
                }

                if (_nextLayer != null && _nextLayer.Exists(oldPath) != VfsEntryExistence.DoesNotExist)
                {
                    CreateScratchTombstone(oldPath);
                    SetPathStatusDirect(oldPath, (ScratchVfsPathStatus.NonexistentTombstoned, VfsEntryExistence.DoesNotExist));
                }
                else
                {
                    // Nothing to do since we did a fast move; old scratch file is gone.
                    SetPathStatusDirect(oldPath, (ScratchVfsPathStatus.Nonexistent, VfsEntryExistence.DoesNotExist));
                }

                SetPathStatusDirect(newPath, (ScratchVfsPathStatus.Materialized, oldType));
                return true;
            }
            else if (oldStatus == ScratchVfsPathStatus.Passthrough)
            {
                if (oldType == VfsEntryExistence.FileExists)
                {
                    // Slow move, since we might have to read down to the base layer which might
                    // not actually have a file sitting behind it.
                    VfsEntry? oldMetadata = null, newMetadata = null;
                    using (var readHandle = OpenFile(oldPath, FileMode.Open, FileAccess.Read, FileShare.Read, ref oldMetadata))
                    {
                        using (var writeHandle = OpenFile(newPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, ref newMetadata))
                        {
                            byte[] buffer = new byte[128 * 1024];
                            for (long offset = 0; offset < readHandle!.VfsFile.Length;)
                            {
                                readHandle.VfsFile.ReadFile(buffer, out uint bytesRead, offset);
                                writeHandle!.VfsFile.WriteFile(buffer, bytesRead, out uint bytesWritten, offset);
                                if (bytesWritten != bytesRead)
                                {
                                    throw new InvalidOperationException("Expected to write as many bytes as were written during MoveFile");
                                }
                                offset += bytesRead;
                            }
                        }
                    }

                    SetPathStatus(oldPath, ScratchVfsPathStatus.NonexistentTombstoned, VfsEntryExistence.DoesNotExist);
                    SetPathStatus(newPath, ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists);
                }
                else if (oldType == VfsEntryExistence.DirectoryExists)
                {
                    throw new NotSupportedException("Directly moving directories is not supported");
                }
                else
                {
                    throw new InvalidOperationException("Unexpected oldType in MoveFile");
                }
            }
            else
            {
                throw new InvalidOperationException("Unexpected oldStatus in MoveFile");
            }

            return true;
        }

        public VfsEntry? GetInfo(string path)
        {
            var scratchPath = GetScratchPath(path);

            var status = GetPathStatus(path);

            if (status.existence == VfsEntryExistence.DoesNotExist)
            {
                return null;
            }

            if (status.status == ScratchVfsPathStatus.Materialized &&
                status.existence == VfsEntryExistence.FileExists)
            {
                var fileInfo = new FileInfo(scratchPath);
                return new VfsEntry
                {
                    Name = fileInfo.Name,
                    CreationTime = fileInfo.CreationTime,
                    LastAccessTime = fileInfo.LastAccessTime,
                    LastWriteTime = fileInfo.LastWriteTime,
                    ChangeTime = fileInfo.LastWriteTime,
                    Attributes = fileInfo.Attributes,
                    Size = fileInfo.Length,
                };
            }
            else if (status.status == ScratchVfsPathStatus.Materialized &&
                status.existence == VfsEntryExistence.DirectoryExists)
            {
                var directoryInfo = new DirectoryInfo(scratchPath);
                return new VfsEntry
                {
                    Name = directoryInfo.Name,
                    CreationTime = directoryInfo.CreationTimeUtc,
                    LastAccessTime = directoryInfo.LastAccessTimeUtc,
                    LastWriteTime = directoryInfo.LastWriteTimeUtc,
                    ChangeTime = directoryInfo.LastWriteTimeUtc,
                    Attributes = directoryInfo.Attributes,
                    Size = 0,
                };
            }
            else
            {
                return _nextLayer?.GetInfo(path);
            }
        }

        public IVfsFileHandle<IVfsFile>? OpenFile(
            string path,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            ref VfsEntry? metadata)
        {
            IVfsFileHandle<IVfsFile>? result = null;
            VfsEntry? metadataResult = null;

            if (!_openAndReadLock.TryLock($"OpenFile:{path},{fileMode},{fileAccess},{fileShare}", NormalizePathKey(path), _deadlockThreshold, () =>
            {
#if ENABLE_TRACE_LOGS
                _logger.LogTrace($"start open: {path} (mode: {fileMode}, access: {fileAccess}, share: {fileShare})");
#endif
                VfsEntry? metadataResultInternal = null;
                result = OpenFileInternal(path, fileMode, fileAccess, fileShare, ref metadataResultInternal, fileMode);
                metadataResult = metadataResultInternal;
#if ENABLE_TRACE_LOGS
                _logger.LogTrace($"end   open: {path} (mode: {fileMode}, access: {fileAccess}, share: {fileShare})");
#endif
            }, out var blockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{blockingContext}' while trying to obtain OpenAndReadLock on OpenFile({path}, ...)!");
            }
            metadata = metadataResult;
            return result;
        }

        private IVfsFileHandle<IVfsFile>? OpenFileInternal(
            string path,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            ref VfsEntry? metadata,
            FileMode realFileMode)
        {
            var scratchPath = GetScratchPath(path);

            var existingStatus = GetPathStatus(path);
            if (existingStatus.existence == VfsEntryExistence.DirectoryExists)
            {
                // This is a directory.
                throw new ArgumentException($"The specified path is a directory, not a file, so calling OpenFile is invalid!", nameof(path));
            }

            switch (fileMode)
            {
                case FileMode.CreateNew:
                    if (existingStatus.existence != VfsEntryExistence.DoesNotExist)
                    {
                        // File exists but we want to create a new one, so don't allow this. Dokan
                        // projection layer will turn this into the correct result due to the fileMode.
                        return null;
                    }
                    var timestamp = DateTime.UtcNow;
                    metadata = new VfsEntry
                    {
                        Name = Path.GetFileName(path),
                        CreationTime = timestamp,
                        LastAccessTime = timestamp,
                        LastWriteTime = timestamp,
                        ChangeTime = timestamp,
                        Attributes = FileAttributes.Archive,
                        Size = 0,
                    };
                    // Make sure we create the parent scratch directory to hold this file so that
                    // WindowsVfsFile does not crash with DirectoryNotFoundException.
                    var parentDirectory = Path.GetDirectoryName(path);
                    if (!string.IsNullOrWhiteSpace(parentDirectory))
                    {
                        if (GetPathStatus(parentDirectory).status != ScratchVfsPathStatus.Materialized)
                        {
                            CreateScratchDirectory(parentDirectory);
                        }
                    }
                    var createFile = _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                    // @note: We don't call SetPathStatus until we know that WindowsVfsFile created
                    // the file successfully, otherwise we can end up in a situation where one thread sets the
                    // path status and then throws an exception in WindowsVfsFile, leaving the path
                    // status cache in a bad state since the file won't actually exist on disk.
                    SetPathStatus(path, ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists);
                    return createFile;
                case FileMode.Create:
                    if (existingStatus.existence != VfsEntryExistence.DoesNotExist)
                    {
                        // Effectively "Truncate".
                        return OpenFileInternal(path, FileMode.Truncate, fileAccess, fileShare, ref metadata, fileMode);
                    }
                    // Effectively "CreateNew".
                    return OpenFileInternal(path, FileMode.CreateNew, fileAccess, fileShare, ref metadata, fileMode);
                case FileMode.Truncate:
                    if (existingStatus.status == ScratchVfsPathStatus.Materialized)
                    {
                        // Return the existing file in truncate mode, the operating system will handle
                        // truncating the existing file due to WindowsVfsFile using direct handles.
                        return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                    }
                    if (existingStatus.status == ScratchVfsPathStatus.Passthrough)
                    {
                        // We are creating a new empty scratch file without copying
                        // the existing data, which is effectively truncating it.
                        SetPathStatus(path, ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists, true);
                        return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                    }
                    // The file doesn't exist.
                    return null;
                case FileMode.Append:
                    if (existingStatus.existence != VfsEntryExistence.DoesNotExist)
                    {
                        // Effectively "Open".
                        return OpenFileInternal(path, FileMode.Open, fileAccess, fileShare, ref metadata, fileMode);
                    }
                    // Effectively "CreateNew".
                    return OpenFileInternal(path, FileMode.CreateNew, fileAccess, fileShare, ref metadata, fileMode);
                case FileMode.OpenOrCreate:
                    if (existingStatus.status == ScratchVfsPathStatus.Materialized)
                    {
                        // Open the existing scratch file.
                        return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                    }
                    if (existingStatus.status == ScratchVfsPathStatus.Passthrough)
                    {
                        // If this is a read-only open, then we can just open the layer below directly.
                        if (fileAccess == FileAccess.Read)
                        {
                            return _nextLayer?.OpenFile(path, realFileMode, fileAccess, fileShare, ref metadata);
                        }
                        // Otherwise we need to do a copy-on-write into the scratch area.
                        else
                        {
                            SetPathStatus(path, ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists);
                            return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                        }
                    }
                    if (fileAccess != FileAccess.Read)
                    {
                        // Otherwise we're creating a new file (and not copying from the base layer).
                        SetPathStatus(path, ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists);
                        return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                    }
                    // Otherwise we're trying to read a file that doesn't exist.
                    return null;
                case FileMode.Open:
                    if (existingStatus.status == ScratchVfsPathStatus.Materialized)
                    {
                        // Return the existing scratch file.
                        var fileInfo = new FileInfo(scratchPath);
                        if (_enableCorrectnessChecks)
                        {
                            try
                            {
                                _ = fileInfo.Length;
                            }
                            catch (FileNotFoundException)
                            {
                                // This is a directory.
                                throw new ArgumentException($"Inconsistent cache state!", nameof(path));
                            }
                        }
                        metadata = new VfsEntry
                        {
                            Name = Path.GetFileName(path),
                            CreationTime = fileInfo.CreationTimeUtc,
                            LastAccessTime = fileInfo.LastAccessTimeUtc,
                            LastWriteTime = fileInfo.LastWriteTimeUtc,
                            ChangeTime = fileInfo.LastWriteTimeUtc,
                            Attributes = fileInfo.Attributes,
                            Size = fileInfo.Length,
                        };
                        return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                    }
                    if (existingStatus.status == ScratchVfsPathStatus.Passthrough)
                    {
                        // If this is a read-only open, then we can just open the layer below directly.
                        if (fileAccess == FileAccess.Read)
                        {
                            // @todo: Technically if the FileShare mode is ReadWrite, and another request materializes this file
                            // and the caller relies on being able to see those writes, then this behaviour is incorrect. If we
                            // need to support that scenario, we'll need to wrap these file objects and "upgrade" them to the scratch
                            // file as soon as the other request materializes it.
                            return _nextLayer?.OpenFile(path, realFileMode, fileAccess, fileShare, ref metadata);
                        }
                        // Otherwise we need to do a copy-on-write into the scratch area.
                        else
                        {
                            // @note: No need to set scratch index here, as the materialization called
                            // from SetPathStatus will handle it for us.
                            SetPathStatus(path, ScratchVfsPathStatus.Materialized, VfsEntryExistence.FileExists);
                            var fileInfo = new FileInfo(scratchPath);
                            metadata = new VfsEntry
                            {
                                Name = Path.GetFileName(path),
                                CreationTime = fileInfo.CreationTimeUtc,
                                LastAccessTime = fileInfo.LastAccessTimeUtc,
                                LastWriteTime = fileInfo.LastWriteTimeUtc,
                                ChangeTime = fileInfo.LastWriteTimeUtc,
                                Attributes = fileInfo.Attributes,
                                Size = fileInfo.Length,
                            };
                            return _localIoVfsFileFactory.CreateVfsFileHandle(scratchPath, realFileMode, fileAccess, fileShare, _fsScratchCache, path);
                        }
                    }
                    // The file doesn't exist (and we aren't creating because we're in Open mode).
                    return null;
                default:
                    Console.WriteLine("error: unknown file mode in scratch layer");
                    return null;
            }
        }

        public bool SetBasicInfo(
            string path,
            uint? attributes,
            DateTimeOffset? creationTime,
            DateTimeOffset? lastAccessTime,
            DateTimeOffset? lastWriteTime,
            DateTimeOffset? changeTime)
        {
            var result = false;

            if (!_openAndReadLock.TryLock("SetBasicInfo", NormalizePathKey(path), _deadlockThreshold, () =>
            {
                var existingStatus = GetPathStatus(path);
                if (existingStatus.status == ScratchVfsPathStatus.Nonexistent ||
                    existingStatus.status == ScratchVfsPathStatus.NonexistentTombstoned ||
                    existingStatus.existence == VfsEntryExistence.DoesNotExist)
                {
                    // This path does not exist.
                    result = false;
                    return;
                }
                else if (existingStatus.status == ScratchVfsPathStatus.Passthrough)
                {
                    // We have to materialize the file so we can set the attributes
                    // on the scratch version.
                    SetPathStatus(path, ScratchVfsPathStatus.Materialized, existingStatus.existence);
                }

                FileSystemInfo fsi;
                if (existingStatus.existence == VfsEntryExistence.FileExists)
                {
                    fsi = new FileInfo(GetScratchPath(path));
                }
                else if (existingStatus.existence == VfsEntryExistence.DirectoryExists)
                {
                    fsi = new DirectoryInfo(GetScratchPath(path));
                }
                else
                {
                    throw new InvalidOperationException("FSI not set");
                }

                if (attributes != null)
                {
                    fsi.Attributes = (FileAttributes)attributes.Value;
                }
                if (creationTime != null)
                {
                    fsi.CreationTimeUtc = creationTime.Value.UtcDateTime;
                }
                if (lastAccessTime != null)
                {
                    fsi.LastAccessTimeUtc = lastAccessTime.Value.UtcDateTime;
                }
                if (lastWriteTime != null)
                {
                    fsi.LastWriteTimeUtc = lastWriteTime.Value.UtcDateTime;
                }
                // @note: We don't do anything with changeTime, but the WinFsp
                // layer properly does. Also, since we're changing the metadata
                // of the scratch file, the actual changeTime should be 
                // the current timestamp.
                //
                // In either case, changeTime doesn't actually seem readable
                // by applications (I mean, we can't get at it in .NET), so I
                // doubt allowing it to be forcibly set to a value is
                // relevant for correct application behaviour.

                _fsScratchCache.OnObjectModifiedAtRelativePath(path);

                result = true;
            }, out var blockingContext))
            {
                throw new VfsLayerDeadlockException($"Blocked by '{blockingContext}' while trying to obtain OpenAndReadLock on SetBasicInfo({path}, ...)!");
            }
            return result;
        }
    }
}
