namespace Redpoint.Uefs.Daemon.RemoteStorage.Pooling
{
    using System.Collections.Concurrent;

    public class FileStreamPool : IDisposable
    {
        private readonly string _path;
        private readonly FileAccess _fileAccess;
        private readonly FileShare _fileShare;
        private readonly FileMode _fileMode;
        private readonly ConcurrentBag<FileStream> _availableFileStreams;
        private readonly ConcurrentBag<FileStream> _allFileStreams;

        public FileStreamPool(string path, FileAccess fileAccess, FileShare fileShare, FileMode fileMode = FileMode.Open)
        {
            _path = path;
            _fileAccess = fileAccess;
            _fileShare = fileShare;
            _fileMode = fileMode;
            _availableFileStreams = new ConcurrentBag<FileStream>();
            _allFileStreams = new ConcurrentBag<FileStream>();
        }

        private class FileStreamPoolAllocation : IFileStreamPoolAllocation
        {
            private readonly FileStreamPool _pool;

            public FileStream FileStream { get; }

            public FileStreamPoolAllocation(FileStreamPool pool, FileStream fileStream)
            {
                _pool = pool;
                FileStream = fileStream;
            }

            public void Dispose()
            {
                _pool._availableFileStreams.Add(FileStream);
            }
        }

        public IFileStreamPoolAllocation Rent()
        {
            FileStream? result;
            if (_availableFileStreams.TryTake(out result))
            {
                return new FileStreamPoolAllocation(this, result);
            }
            result = new FileStream(_path, _fileMode, _fileAccess, _fileShare, 4096);
            _allFileStreams.Add(result);
            return new FileStreamPoolAllocation(this, result);
        }

        public void Dispose()
        {
            foreach (var fs in _allFileStreams)
            {
                fs.Dispose();
            }
            _allFileStreams.Clear();
            _availableFileStreams.Clear();
        }
    }
}
