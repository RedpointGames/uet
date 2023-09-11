namespace Redpoint.Uefs.Daemon.RemoteStorage.Pooling
{
    using Microsoft.Win32.SafeHandles;
    using System.Collections.Concurrent;

    public class SafeFileHandlePool
    {
        private readonly string _path;
        private readonly FileAccess _fileAccess;
        private readonly FileShare _fileShare;
        private readonly FileMode _fileMode;
        private readonly ConcurrentBag<SafeFileHandlePoolAllocation> _availableSafeFileHandles;
        private readonly ConcurrentBag<SafeFileHandle> _allSafeFileHandles;

        public SafeFileHandlePool(string path, FileAccess fileAccess, FileShare fileShare, FileMode fileMode = FileMode.Open)
        {
            _path = path;
            _fileAccess = fileAccess;
            _fileShare = fileShare;
            _fileMode = fileMode;
            _availableSafeFileHandles = new ConcurrentBag<SafeFileHandlePoolAllocation>();
            _allSafeFileHandles = new ConcurrentBag<SafeFileHandle>();
        }

        private sealed class SafeFileHandlePoolAllocation : ISafeFileHandlePoolAllocation
        {
            private readonly SafeFileHandlePool _pool;

            public SafeFileHandle SafeFileHandle { get; }

            public SafeFileHandlePoolAllocation(SafeFileHandlePool pool, SafeFileHandle safeFileHandle)
            {
                _pool = pool;
                SafeFileHandle = safeFileHandle;
            }

            public void Dispose()
            {
                _pool._availableSafeFileHandles.Add(this);
            }
        }

        public ISafeFileHandlePoolAllocation Rent()
        {
            SafeFileHandlePoolAllocation? result;
            if (_availableSafeFileHandles.TryTake(out result))
            {
                return result;
            }
            var handle = File.OpenHandle(_path, _fileMode, _fileAccess, _fileShare);
            _allSafeFileHandles.Add(handle);
            return new SafeFileHandlePoolAllocation(this, handle);
        }

        public void Dispose()
        {
            foreach (var handle in _allSafeFileHandles)
            {
                handle.Dispose();
            }
            _allSafeFileHandles.Clear();
            _availableSafeFileHandles.Clear();
        }
    }
}
