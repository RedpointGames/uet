namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Docker.Registry.DotNet.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Daemon.PackageFs.CachingStorage;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Driver;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.Versioning;
    using System.Text.Json;
    using System.Threading.Tasks;

    [SupportedOSPlatform("windows6.2")]
    internal class VfsPackageFs : CachingPackageFs
    {
        private readonly IVfsDriverFactory _vfsFactory;
        private readonly ILogger<IVfsLayer> _logger;
        private readonly IRemoteStorage<ManifestLayer> _registryRemoteStorage;
        private readonly IRemoteStorage<RegistryReferenceInfo> _referenceRemoteStorage;
        private readonly CachedFilePool _cachedFilePool;
        private readonly string _storagePath;

        private IDisposable? _vfs = null;

        public VfsPackageFs(
            IVfsDriverFactory vfsFactory,
            ILogger<IVfsLayer> logger,
            IRemoteStorage<ManifestLayer> registryRemoteStorage,
            IRemoteStorage<RegistryReferenceInfo> referenceRemoteStorage,
            string storagePath) : base(logger, storagePath)
        {
            _vfsFactory = vfsFactory;
            _logger = logger;
            _registryRemoteStorage = registryRemoteStorage;
            _referenceRemoteStorage = referenceRemoteStorage;
            _cachedFilePool = new CachedFilePool(
                logger,
                Path.Combine(
                    storagePath,
                    "hostpkgs",
                    "cache"));
            _storagePath = storagePath;

            Init();
        }

        protected override void Mount()
        {
            _vfs = _vfsFactory.InitializeAndMount(
                new StorageProjectionLayer(
                    _logger,
                    this,
                    _storagePath),
                GetVFSMountPath(),
                null);
        }

        protected override void Unmount()
        {
            _vfs?.Dispose();
            _vfs = null;
        }

        protected override Task<bool> VerifyPackageAsync(
            bool isFixing,
            string normalizedPackageHash,
            CachingInfoJson info,
            Action<Action<PollingResponse>> updatePollingResponse)
        {
            // Open the remote resource.
            IRemoteStorageBlobFactory blobFactory;
            switch (info!.Type)
            {
                case "reference":
                    blobFactory = _referenceRemoteStorage.GetFactory(JsonSerializer.Deserialize(info.SerializedObject, UefsRegistryJsonSerializerContext.Default.RegistryReferenceInfo)!);
                    break;
                case "registry":
                    blobFactory = _registryRemoteStorage.GetFactory(JsonSerializer.Deserialize(info.SerializedObject, PackageFsJsonSerializerContext.Default.ManifestLayer)!);
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported info type for {normalizedPackageHash}: {info!.Type}");
            }

            // Open the cache file so we can verify it.
            using (var cache = _cachedFilePool.Open(blobFactory, normalizedPackageHash))
            {
                // This will set the error in the operation if it fails.
                var didFix = cache.VfsFile.VerifyChunks(isFixing, updatePollingResponse);

                if (isFixing)
                {
                    // Immediately flush all indexes to disk, ensuring that they are in-sync before the verification process returns.
                    _cachedFilePool.FlushImmediately();
                }

                if (!didFix)
                {
                    return Task.FromResult(false);
                }
            }

            return Task.FromResult(true);
        }

        private class StorageProjectionLayer : IVfsLayer
        {
            private readonly ILogger<IVfsLayer> _logger;
            private readonly VfsPackageFs _storageFS;

            private readonly DateTimeOffset _timestamp;
            private readonly string _infoStoragePath;
            private readonly DirectoryInfo _infoDirectoryInfo;
            private readonly ConcurrentDictionary<string, IRemoteStorageBlobFactory> _resolvedBlobs;

            public StorageProjectionLayer(
                ILogger<IVfsLayer> logger,
                VfsPackageFs storageFS,
                string storagePath)
            {
                _logger = logger;
                _storageFS = storageFS;

                _timestamp = DateTimeOffset.UtcNow;
                _infoStoragePath = Path.Combine(
                    storagePath,
                    "hostpkgs",
                    "info");
                _infoDirectoryInfo = new DirectoryInfo(_infoStoragePath);

                _resolvedBlobs = new ConcurrentDictionary<string, IRemoteStorageBlobFactory>();
            }

            public bool ReadOnly => true;

            public void Dispose()
            {
            }

            public VfsEntryExistence Exists(string path)
            {
                var fileExists = List(string.Empty)!.Any(x => string.Compare(x.Name, path, true) == 0);
                if (!fileExists)
                {
                    _logger.LogWarning($"VHD on-demand storage layer: Requested file does not exist (FileExists): {path}");
                    return VfsEntryExistence.DoesNotExist;
                }

                return VfsEntryExistence.FileExists;
            }

            public VfsEntry? GetInfo(string path)
            {
                var entry = List(string.Empty)!.FirstOrDefault(x => string.Compare(x.Name, path, true) == 0);
                if (entry == null)
                {
                    _logger.LogWarning($"VHD on-demand storage layer: Requested file does not exist (GetInfo): {path}");
                }
                return entry;
            }

            public IEnumerable<VfsEntry>? List(string path)
            {
                if (path != string.Empty)
                {
                    yield break;
                }

                foreach (var file in _infoDirectoryInfo.GetFiles("*.info"))
                {
                    var info = JsonSerializer.Deserialize(
                        File.ReadAllText(file.FullName).Trim(),
                        PackageFsInternalJsonSerializerContext.Default.CachingInfoJson);
                    yield return new VfsEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(file.Name) + ".vhd",
                        Attributes = FileAttributes.Archive,
                        CreationTime = _timestamp,
                        LastAccessTime = _timestamp,
                        LastWriteTime = _timestamp,
                        ChangeTime = _timestamp,
                        Size = info!.Length,
                    };
                }
            }

            public IVfsFileHandle<IVfsFile>? OpenFile(string path, FileMode fileMode, FileAccess fileAccess, FileShare fileShare, ref VfsEntry? metadata)
            {
                var filename = path;

                var blobFactory = _resolvedBlobs.GetOrAdd(filename, _ =>
                {
                    var info = JsonSerializer.Deserialize(
                        File.ReadAllText(Path.Combine(
                            _infoStoragePath,
                            Path.GetFileNameWithoutExtension(filename) + ".info")).Trim(),
                        PackageFsInternalJsonSerializerContext.Default.CachingInfoJson);

                    IRemoteStorageBlobFactory blobFactory;
                    switch (info!.Type)
                    {
                        case "reference":
                            blobFactory = _storageFS._referenceRemoteStorage.GetFactory(JsonSerializer.Deserialize(
                                info.SerializedObject,
                                UefsRegistryJsonSerializerContext.Default.RegistryReferenceInfo)!);
                            break;
                        case "registry":
                            blobFactory = _storageFS._registryRemoteStorage.GetFactory(JsonSerializer.Deserialize(
                                info.SerializedObject,
                                PackageFsJsonSerializerContext.Default.ManifestLayer)!);
                            break;
                        default:
                            throw new InvalidOperationException($"Unsupported info type for {filename}: {info!.Type}");
                    }
                    return blobFactory;
                });

                var handle = _storageFS._cachedFilePool.Open(blobFactory, Path.GetFileNameWithoutExtension(filename));
                metadata = new VfsEntry
                {
                    Name = filename,
                    Attributes = FileAttributes.Archive,
                    CreationTime = _timestamp,
                    LastAccessTime = _timestamp,
                    LastWriteTime = _timestamp,
                    ChangeTime = _timestamp,
                    Size = handle.VfsFile.Length,
                };
                return handle;
            }

            #region Unsupported Write Methods

            public bool CreateDirectory(string path)
            {
                _logger.LogError("Creating directories is not permitted in the storage projection layer.");
                return false;
            }

            public bool DeleteDirectory(string path)
            {
                _logger.LogError("Deleting directories is not permitted in the storage projection layer.");
                return false;
            }

            public bool DeleteFile(string path)
            {
                _logger.LogError("Deleting files is not permitted in the storage projection layer.");
                return false;
            }

            public bool MoveFile(string oldPath, string newPath, bool replace)
            {
                _logger.LogError("Moving files is not permitted in the storage projection layer.");
                return false;
            }

            public bool SetBasicInfo(string path, uint? attributes, DateTimeOffset? creationTime, DateTimeOffset? lastAccessTime, DateTimeOffset? lastWriteTime, DateTimeOffset? changeTime)
            {
                _logger.LogError("Setting file information is not permitted in the storage projection layer.");
                return false;
            }

            #endregion
        }
    }
}
