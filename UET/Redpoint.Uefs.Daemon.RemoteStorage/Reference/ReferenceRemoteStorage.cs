namespace Redpoint.Uefs.Daemon.RemoteStorage.Reference
{
    using Microsoft.Win32.SafeHandles;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Daemon.RemoteStorage.Pooling;

    public class ReferenceRemoteStorage : IRemoteStorage<RegistryReferenceInfo>
    {
        public string Type => "reference";

        public IRemoteStorageBlobFactory GetFactory(RegistryReferenceInfo reference)
        {
            ArgumentNullException.ThrowIfNull(reference);
            return new ReferenceRemoteStorageBlobFactory(reference.Location);
        }

        private sealed class ReferenceRemoteStorageBlobFactory : IRemoteStorageBlobFactory
        {
            private readonly FileStreamPool _pool;
            private readonly SafeFileHandlePool _sfPool;

            public ReferenceRemoteStorageBlobFactory(string path)
            {
                if (OperatingSystem.IsWindows() && path.StartsWith("\\\\", StringComparison.Ordinal) ||
                    OperatingSystem.IsMacOS() && path.StartsWith('/'))
                {
                    // @note: We trying to use 'FileShare.ReadWrite | FileShare.Delete' to workaround an issue on macOS where the macOS SMB client fails to open a file that has FileShare.Read even though it should otherwise succeed due to these failures:
                    //
                    // smbfs_add_update_lease: Cant add lease because found existing lease on <5.4.0-launcher.sparseimage> during <Open>?
                    // smbfs_open: smbfs_add_update_lease add failed 37
                    _pool = new FileStreamPool(path, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileMode.Open);
                    _sfPool = new SafeFileHandlePool(path, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, FileMode.Open);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported reference location in registry.");
                }
            }

            public void Dispose()
            {
                _pool.Dispose();
            }

            public IRemoteStorageBlob Open()
            {
                return new ReferenceRemoteStorageBlob(_pool.Rent());
            }

            public IRemoteStorageBlobUnsafe OpenUnsafe()
            {
                return new ReferenceRemoteStorageBlobUnsafe(_sfPool.Rent());
            }
        }

        internal sealed class ReferenceRemoteStorageBlob : IRemoteStorageBlob
        {
            private IFileStreamPoolAllocation _allocation;

            public ReferenceRemoteStorageBlob(IFileStreamPoolAllocation allocation)
            {
                _allocation = allocation;
            }

            public long Position
            {
                get { return _allocation.FileStream.Position; }
                set { _allocation.FileStream.Position = value; }
            }

            public long Length => _allocation.FileStream.Length;

            public void Dispose()
            {
                _allocation.Dispose();
            }

            public int Read(byte[] buffer, int offset, int length)
            {
                return _allocation.FileStream.Read(buffer, offset, length);
            }

            public Task<int> ReadAsync(byte[] buffer, int offset, int length)
            {
                return _allocation.FileStream.ReadAsync(buffer, offset, length);
            }
        }

        internal sealed class ReferenceRemoteStorageBlobUnsafe : IRemoteStorageBlobUnsafe
        {
            private ISafeFileHandlePoolAllocation _allocation;

            public ReferenceRemoteStorageBlobUnsafe(ISafeFileHandlePoolAllocation allocation)
            {
                _allocation = allocation;
            }

            public SafeFileHandle SafeFileHandle => _allocation.SafeFileHandle;

            public void Dispose()
            {
                _allocation.Dispose();
            }
        }
    }
}
