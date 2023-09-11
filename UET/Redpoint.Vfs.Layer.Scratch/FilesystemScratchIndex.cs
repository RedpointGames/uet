namespace Redpoint.Vfs.Layer.Scratch
{
    using Microsoft.Extensions.Logging;
    using System;

    internal class FilesystemScratchIndex : IDisposable
    {
        private readonly ILogger<FilesystemScratchIndex> _logger;
        private readonly FilesystemScratchCache _fsScratchCache;
        private ScratchIndex _scratchIndex;

        public const byte Status_NotSet = 0;
        public const byte Status_ExistsDir = 1;
        public const byte Status_ExistsFile = 2;
        public const byte Status_Tombstoned = 3;

        public FilesystemScratchIndex(
            ILogger<FilesystemScratchIndex> logger,
            FilesystemScratchCache fsScratchCache,
            string path,
            string rootPath)
        {
            _logger = logger;
            _fsScratchCache = fsScratchCache;

            try
            {
                _scratchIndex = new ScratchIndex(path);
            }
            catch (ScratchIndexCorruptException)
            {
                _logger.LogWarning("Scratch index is corrupt; rebuilding based on files that are present in the scratch folder. Tombstones will be lost.");

                var rebuildPath = $"{path}.rebuild";
                if (Directory.Exists(rebuildPath))
                {
                    Directory.Delete(rebuildPath, true);
                }
                var rebuildIndex = new ScratchIndex(rebuildPath);
                RebuildIndex(rebuildIndex, string.Empty, new DirectoryInfo(rootPath));
                rebuildIndex.Dispose();

                Directory.Move(path, $"{path}.old");
                try
                {
                    Directory.Move(rebuildPath, path);
                }
                catch
                {
                    Directory.Move($"{path}.old", path);
                    throw;
                }
                Directory.Delete($"{path}.old", true);

                _logger.LogInformation("Scratch index rebuild is now complete.");

                _scratchIndex = new ScratchIndex(path);
            }
        }

        private static void RebuildIndex(ScratchIndex scratchIndex, string currentPath, DirectoryInfo di)
        {
            foreach (var sdi in di.GetDirectories())
            {
                if (sdi.Name == ".uefs.db")
                {
                    continue;
                }

                scratchIndex.Set(currentPath + sdi.Name.ToLowerInvariant(), Status_ExistsDir);

                RebuildIndex(scratchIndex, currentPath + sdi.Name.ToLowerInvariant() + "\\", sdi);
            }
            foreach (var fi in di.GetFiles())
            {
                scratchIndex.Set(currentPath + fi.Name.ToLowerInvariant(), Status_ExistsFile);
            }
        }

        private string NormalizePathKey(string path)
        {
            return path.ToLowerInvariant();
        }

        public void SetScratchIndex(string path, byte scratchStatus)
        {
            if (scratchStatus == Status_NotSet)
            {
                throw new ArgumentException("Must not pass _scratchIndex_NotSet", nameof(scratchStatus));
            }
            var normalizedPath = NormalizePathKey(path);
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Scratch index: Set: {normalizedPath} = {scratchStatus}");
#endif
            _scratchIndex.Set(normalizedPath, scratchStatus);
            _fsScratchCache.OnObjectModifiedAtRelativePath(normalizedPath);
        }

        public void ClearScratchIndex(string path)
        {
            var normalizedPath = NormalizePathKey(path);
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Scratch index: Clear: {normalizedPath}");
#endif
            _scratchIndex.Delete(normalizedPath);
            _fsScratchCache.OnObjectModifiedAtRelativePath(normalizedPath);
        }

        public void ClearScratchIndexRecursive(string path)
        {
            var normalizedPathKey = NormalizePathKey(path);
            foreach (var key in _scratchIndex.IterateKeysOnly())
            {
                if (key.StartsWith(normalizedPathKey + '\\'))
                {
#if ENABLE_TRACE_LOGS
                    _logger.LogTrace($"Scratch index: Clear: {key}");
#endif
                    _scratchIndex.Delete(key);
                    _fsScratchCache.OnObjectModifiedAtRelativePath(key);
                }
            }
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Scratch index: Clear: {normalizedPathKey}");
#endif
            _scratchIndex.Delete(normalizedPathKey);
            _fsScratchCache.OnObjectModifiedAtRelativePath(normalizedPathKey);
        }

        public void MoveScratchIndexRecursive(string oldPath, string newPath)
        {
            var normalizedOldPath = NormalizePathKey(oldPath);
            var normalizedNewPath = NormalizePathKey(newPath);
            foreach (var kv in _scratchIndex.Iterate())
            {
                if (kv.normalizedPath.StartsWith(normalizedOldPath + '\\'))
                {
                    var newKey = normalizedNewPath + kv.normalizedPath[normalizedOldPath.Length..];
#if ENABLE_TRACE_LOGS
                    _logger.LogTrace($"Scratch index: Move: {kv.normalizedPath} -> {newKey}");
#endif
                    _scratchIndex.Delete(kv.normalizedPath);
                    _scratchIndex.Set(newKey, kv.status);
                    _fsScratchCache.OnObjectModifiedAtRelativePath(kv.normalizedPath);
                    _fsScratchCache.OnObjectModifiedAtRelativePath(newKey);
                }
                else if (kv.normalizedPath == normalizedOldPath)
                {
#if ENABLE_TRACE_LOGS
                    _logger.LogTrace($"Scratch index: Move: {normalizedOldPath} -> {normalizedNewPath}");
#endif
                    _scratchIndex.Delete(normalizedOldPath);
                    _scratchIndex.Set(normalizedNewPath, kv.status);
                    _fsScratchCache.OnObjectModifiedAtRelativePath(normalizedOldPath);
                    _fsScratchCache.OnObjectModifiedAtRelativePath(normalizedNewPath);
                }
            }
        }

        public byte GetScratchIndex(string path)
        {
            var normalizedPath = NormalizePathKey(path);
            var result = _scratchIndex.Get(normalizedPath);
            if (result.found)
            {
#if ENABLE_TRACE_LOGS
                _logger.LogTrace($"Scratch index: Get: {normalizedPath} = {result.status}");
#endif
                return result.status;
            }
#if ENABLE_TRACE_LOGS
            _logger.LogTrace($"Scratch index: Get: {normalizedPath} = <not set>");
#endif
            return Status_NotSet;
        }

        public void Dispose()
        {
            _scratchIndex.Dispose();
        }
    }
}
