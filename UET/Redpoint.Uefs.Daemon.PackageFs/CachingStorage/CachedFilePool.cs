namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Vfs.Abstractions;
    using System.Collections.Concurrent;
    using System.Diagnostics.CodeAnalysis;
    using System.Runtime.Versioning;

    [SupportedOSPlatform("windows6.2")]
    internal sealed class CachedFilePool
    {
        private readonly ILogger _logger;
        private readonly string _localStoragePath;
        private ConcurrentDictionary<string, OpenedCachedFile> _openCachedFiles;
        private Task _flushingTask;

        [SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Lifetime of fields is managed separate from the Dispose pattern.")]
        private sealed class OpenedCachedFile
        {
            private readonly ILogger _logger;
            private readonly IRemoteStorageBlobFactory _sourceFactory;
            private readonly string _cachePath;
            private readonly string _indexPath;
            private readonly Concurrency.Semaphore _flushLock = new Concurrency.Semaphore(1);
            private readonly object _lock = new object();

            private CachedFile? _file;
            private long _openHandles;

            public OpenedCachedFile(
                ILogger logger,
                IRemoteStorageBlobFactory sourceFactory,
                string cachePath,
                string indexPath)
            {
                _logger = logger;
                _sourceFactory = sourceFactory;
                _cachePath = cachePath;
                _indexPath = indexPath;

                _file = null;
                _openHandles = 0;
            }

            public void FlushIndex()
            {
                _flushLock.Wait(CancellationToken.None);
                try
                {
                    _file?.FlushIndex();
                }
                finally
                {
                    _flushLock.Release();
                }
            }

            private sealed class OpenedCachedFileHandle : IVfsFileHandle<ICachedFile>
            {
                private readonly OpenedCachedFile _ocf;

                public OpenedCachedFileHandle(OpenedCachedFile ocf)
                {
                    _ocf = ocf;
                }

                public ICachedFile VfsFile => _ocf._file!;

                public void Dispose()
                {
                    lock (_ocf._lock)
                    {
                        _ocf._openHandles--;
                        if (_ocf._openHandles == 0)
                        {
                            _ocf._logger.LogInformation($"Cache file closing: {_ocf._cachePath} / {_ocf._indexPath}");
                            _ocf._flushLock.Wait(CancellationToken.None);
                            try
                            {
                                _ocf._file?.FlushIndex();
                            }
                            finally
                            {
                                _ocf._flushLock.Release();
                            }
                            _ocf._file?.Dispose();
                            _ocf._file = null;
                            _ocf._logger.LogInformation($"Cache file closed: {_ocf._cachePath} / {_ocf._indexPath}");
                        }
                    }
                }
            }

            public IVfsFileHandle<ICachedFile> Allocate()
            {
                lock (_lock)
                {
                    _openHandles++;
                    if (_openHandles == 1)
                    {
                        _logger.LogInformation($"Cache file opening: {_cachePath} / {_indexPath}");
                        _file = new CachedFile(_logger, _sourceFactory, _cachePath, _indexPath);
                        _logger.LogInformation($"Cache file opened: {_cachePath} / {_indexPath}");
                    }
                    return new OpenedCachedFileHandle(this);
                }
            }
        }

        private string GetCachePath(string id)
        {
            return Path.Combine(_localStoragePath, $"{id}.data");
        }

        private string GetIndexPath(string id)
        {
            return Path.Combine(_localStoragePath, $"{id}.index");
        }

        public CachedFilePool(ILogger logger, string localStoragePath)
        {
            _logger = logger;
            _localStoragePath = localStoragePath;
            _openCachedFiles = new ConcurrentDictionary<string, OpenedCachedFile>();

            _flushingTask = Task.Run(FlushingLoop);
        }

        public IVfsFileHandle<ICachedFile> Open(IRemoteStorageBlobFactory blobFactory, string id)
        {
            return _openCachedFiles.GetOrAdd(
                id, _ => new OpenedCachedFile(
                    _logger,
                    blobFactory,
                    GetCachePath(id),
                    GetIndexPath(id))).Allocate();
        }

        public void FlushImmediately()
        {
            foreach (var file in _openCachedFiles.Values)
            {
                file.FlushIndex();
            }
        }

        private async Task FlushingLoop()
        {
            do
            {
                foreach (var file in _openCachedFiles.Values)
                {
                    file.FlushIndex();
                }
                await Task.Delay(5 * 1000).ConfigureAwait(false);
            }
            while (true);
        }
    }
}
