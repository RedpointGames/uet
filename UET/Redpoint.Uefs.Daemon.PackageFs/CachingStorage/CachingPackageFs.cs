namespace Redpoint.Uefs.Daemon.PackageFs.CachingStorage
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Uefs.Daemon.PackageFs.Tagging;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Protocol;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;

    internal abstract class CachingPackageFs : IPackageFs
    {
        private readonly ILogger _logger;

        private readonly string _vfsmountStoragePath;

        private readonly string _cacheStoragePath;

        private readonly string _infoStoragePath;

        private readonly string _tagStoragePath;

        private Dictionary<string, CachingInfoJson> _currentInfos = new Dictionary<string, CachingInfoJson>();

        private Dictionary<string, PackageStorageTag> _tagReferences = new Dictionary<string, PackageStorageTag>();

        protected CachingPackageFs(
            ILogger logger,
            string storagePath)
        {
            _logger = logger;

            _vfsmountStoragePath = Path.Combine(
                storagePath,
                "hostpkgs",
                "vfsmount");
            Directory.CreateDirectory(_vfsmountStoragePath);

            _cacheStoragePath = Path.Combine(
                storagePath,
                "hostpkgs",
                "cache");
            Directory.CreateDirectory(_cacheStoragePath);

            _infoStoragePath = Path.Combine(
                storagePath,
                "hostpkgs",
                "info");
            Directory.CreateDirectory(_infoStoragePath);

            _tagStoragePath = Path.Combine(
                storagePath,
                "hostpkgs",
                "tags");
            Directory.CreateDirectory(_tagStoragePath);
        }

        protected void Init()
        {
            PurgeDanglingPackages();

            // Load all of the tags.
            foreach (var file in new DirectoryInfo(_tagStoragePath).GetFiles())
            {
                if (file.Extension == ".tag")
                {
                    var info = JsonSerializer.Deserialize(File.ReadAllText(file.FullName).Trim(), PackageFsInternalJsonSerializerContext.Default.PackageStorageTag);
                    if (File.Exists(Path.Combine(_infoStoragePath, info!.Hash + ".info")))
                    {
                        string tagHash;
                        using (var hasher = SHA256.Create())
                        {
                            tagHash = BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(info!.Tag))).Replace("-", "").ToLowerInvariant();
                        }
                        if (tagHash == Path.GetFileNameWithoutExtension(file.Name))
                        {
                            _tagReferences.Add(tagHash, info);
                        }
                    }
                    else
                    {
                        // Invalid tag, delete it.
                        File.Delete(file.FullName);
                    }
                }
            }

            // Load all of the info objects.
            foreach (var file in new DirectoryInfo(_infoStoragePath).GetFiles())
            {
                if (file.Extension == ".info")
                {
                    var info = JsonSerializer.Deserialize(File.ReadAllText(file.FullName).Trim(), PackageFsInternalJsonSerializerContext.Default.CachingInfoJson);
                    var hash = Path.GetFileNameWithoutExtension(file.Name);
                    _currentInfos.Add(hash, info!);
                }
            }

            // Mount the implementation. The implementation is expected
            // to call GetVFSMountPath().
            Mount();
        }

        protected string GetVFSMountPath()
        {
            return _vfsmountStoragePath;
        }

        protected abstract void Mount();

        protected abstract void Unmount();

        public void Dispose()
        {
            // Unmount the implementation.
            Unmount();
        }

        public async Task<string> PullAsync<T>(
            IRemoteStorageBlobFactory remoteStorageBlobFactory,
            string remoteStorageType,
            T remoteStorageReference,
            JsonTypeInfo<T> remoteStorageTypeInfo,
            string packageDigest,
            string extension,
            string tagHash,
            string tag,
            Action releaseGlobalPullLock,
            Action<Action<PollingResponse>, string?> updatePollingResponse)
        {
            // Check to see if we already have this package on disk.
            var normalizedPackageHash = packageDigest.Replace(":", "_");
            if (_currentInfos.ContainsKey(normalizedPackageHash) &&
                File.Exists(Path.Combine(_infoStoragePath, normalizedPackageHash + ".info")))
            {
                // We do. Check if we need to update our tag data.
                if (_tagReferences.ContainsKey(tagHash) &&
                    File.Exists(Path.Combine(_tagStoragePath, tagHash + ".tag")))
                {
                    if (_tagReferences[tagHash].Hash != normalizedPackageHash)
                    {
                        // Update the tag information on disk.
                        _tagReferences[tagHash].Hash = normalizedPackageHash;
                        await File.WriteAllTextAsync(
                            Path.Combine(_tagStoragePath, tagHash + ".tag"),
                            JsonSerializer.Serialize(
                                _tagReferences[tagHash],
                                PackageFsInternalJsonSerializerContext.Default.PackageStorageTag));
                    }
                }
                else
                {
                    // Add our new tag reference.
                    _tagReferences[tagHash] = new PackageStorageTag
                    {
                        Tag = tag,
                        Hash = normalizedPackageHash,
                    };
                    await File.WriteAllTextAsync(
                        Path.Combine(_tagStoragePath, tagHash + ".tag"),
                        JsonSerializer.Serialize(
                            _tagReferences[tagHash],
                                PackageFsInternalJsonSerializerContext.Default.PackageStorageTag));
                }

                var earlyResultPath = Path.Combine(_vfsmountStoragePath, normalizedPackageHash + extension);
                updatePollingResponse(
                    x =>
                    {
                        x.CompleteForPackage(earlyResultPath, normalizedPackageHash);
                    },
                    earlyResultPath);
                _logger.LogInformation($"skipping pull for {tag}, the on-disk copy is already up-to-date");
                return earlyResultPath;
            }

            // If we already have this tag hash, delete it as it's now out of date.
            if (_tagReferences.ContainsKey(tagHash))
            {
                if (File.Exists(Path.Combine(_tagStoragePath, tagHash + ".tag")))
                {
                    File.Delete(Path.Combine(_tagStoragePath, tagHash + ".tag"));
                }
                _tagReferences.Remove(tagHash);
            }

            // Clean up any unused cache files (those that aren't referenced by anything), in case
            // we need to get more space on disk.
            PurgeDanglingPackages();

            // Store the info so the caching layer can get it on-demand.
            _logger.LogInformation($"need to configure '{normalizedPackageHash}' as it does not exist on disk");
            var infoTargetPath = Path.Combine(_infoStoragePath, normalizedPackageHash + ".info");
            long length;
            using (var blob = remoteStorageBlobFactory.Open())
            {
                length = blob.Length;
            }
            var info = new CachingInfoJson
            {
                Type = remoteStorageType,
                SerializedObject = JsonSerializer.Serialize(remoteStorageReference, remoteStorageTypeInfo),
                Length = length
            };
            await File.WriteAllTextAsync(
                infoTargetPath,
                JsonSerializer.Serialize(
                    info,
                    PackageFsInternalJsonSerializerContext.Default.CachingInfoJson));

            // We now have this info on disk.
            _currentInfos.Add(normalizedPackageHash, info);
            _tagReferences[tagHash] = new PackageStorageTag
            {
                Hash = normalizedPackageHash,
                Tag = tag
            };
            await File.WriteAllTextAsync(
                Path.Combine(_tagStoragePath, tagHash + ".tag"),
                JsonSerializer.Serialize(
                    _tagReferences[tagHash],
                    PackageFsInternalJsonSerializerContext.Default.PackageStorageTag));

            // Return the finished operation.
            var resultPath = Path.Combine(_vfsmountStoragePath, normalizedPackageHash + extension);
            updatePollingResponse(
                x =>
                {
                    x.CompleteForPackageWithLength(resultPath, normalizedPackageHash, length);
                },
                resultPath);
            return resultPath;
        }

        public async Task VerifyAsync(
            bool isFixing,
            Action releaseGlobalPullLock,
            Action<Action<PollingResponse>> updatePollingResponse)
        {
            var cachedInfos = _currentInfos.ToArray();
            releaseGlobalPullLock();
            updatePollingResponse(x =>
            {
                x.VerifyingPackages(cachedInfos.Length);
            });
            for (int v = 0; v < cachedInfos.Length; v++)
            {
                var info = cachedInfos[v];
                updatePollingResponse(x =>
                {
                    x.VerifyingPackage(v);
                });
                var didError = false;
                await Task.Run(async () =>
                {
                    try
                    {
                        if (!(await VerifyPackageAsync(isFixing, info.Key, info.Value, updatePollingResponse)))
                        {
                            didError = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        updatePollingResponse(x =>
                        {
                            x.Error($"Exception while verifying {info.Key}: {ex}");
                        });
                        didError = true;
                    }
                });
                if (didError)
                {
                    return;
                }
            }
            updatePollingResponse(x =>
            {
                x.CompleteForVerifying();
            });
        }

        protected abstract Task<bool> VerifyPackageAsync(
            bool isFixing,
            string normalizedPackageHash,
            CachingInfoJson info,
            Action<Action<PollingResponse>> updatePollingResponse);

        private void PurgeDanglingPackages()
        {
            // Scan all of the tags.
            var tagReferences = new Dictionary<string, PackageStorageTag>();
            foreach (var file in new DirectoryInfo(_tagStoragePath).GetFiles())
            {
                if (file.Extension == ".tag")
                {
                    var info = JsonSerializer.Deserialize(
                        File.ReadAllText(file.FullName).Trim(),
                        PackageFsInternalJsonSerializerContext.Default.PackageStorageTag);
                    if (File.Exists(Path.Combine(_infoStoragePath, info!.Hash + ".info")))
                    {
                        string tagHash;
                        using (var hasher = SHA256.Create())
                        {
                            tagHash = BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(info!.Tag))).Replace("-", "").ToLowerInvariant();
                        }
                        if (tagHash == Path.GetFileNameWithoutExtension(file.Name))
                        {
                            tagReferences.Add(tagHash, info);
                        }
                    }
                    else
                    {
                        // Invalid tag, delete it.
                        _logger.LogInformation($"Automatically deleted invalid tag: {file.FullName}");
                        File.Delete(file.FullName);
                    }
                }
            }

            // Scan all of the infos. These hold the actual remote storage data for the images
            // the tags point to.
            var infoReferences = new Dictionary<string, CachingInfoJson>();
            var infoHashes = new HashSet<string>();
            foreach (var file in new DirectoryInfo(_infoStoragePath).GetFiles())
            {
                if (file.Extension == ".info")
                {
                    var info = JsonSerializer.Deserialize(
                        File.ReadAllText(file.FullName).Trim(),
                        PackageFsInternalJsonSerializerContext.Default.CachingInfoJson);
                    var hash = Path.GetFileNameWithoutExtension(file.Name);
                    infoReferences.Add(hash, info!);
                    infoHashes.Add(hash);
                }
            }

            // Scan all of the cached data. Any data or index files that don't have a filename
            // that matches the info hashes gets deleted.
            var filesToDelete = new List<string>();
            foreach (var file in new DirectoryInfo(_cacheStoragePath).GetFiles())
            {
                if (file.Extension == ".data" ||
                    file.Extension == ".index")
                {
                    var hash = Path.GetFileNameWithoutExtension(file.Name);
                    if (!infoHashes.Contains(hash))
                    {
                        filesToDelete.Add(file.FullName);
                    }
                }
            }
            foreach (var fileToDelete in filesToDelete)
            {
                _logger.LogInformation($"Automatically deleted dangling file: {fileToDelete}");
                File.Delete(fileToDelete);
            }
        }
    }
}