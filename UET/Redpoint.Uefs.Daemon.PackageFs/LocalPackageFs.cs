namespace Redpoint.Uefs.Daemon.PackageFs
{
    using Microsoft.Extensions.Logging;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using System.Text.Json.Serialization.Metadata;
    using Redpoint.Uefs.Daemon.PackageFs.Tagging;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Hashing;
    using System.Buffers;

    internal sealed class LocalPackageFs : IPackageFs
    {
        private readonly ILogger<LocalPackageFs> _logger;
        private readonly string _storagePath;

        private HashSet<string> _availablePackages = new HashSet<string>();
        private Dictionary<string, PackageStorageTag> _tagReferences = new Dictionary<string, PackageStorageTag>();

        public LocalPackageFs(
            ILogger<LocalPackageFs> logger,
            string storagePath)
        {
            _logger = logger;

            _storagePath = Path.Combine(
                storagePath,
                "hostpkgs");
            Directory.CreateDirectory(_storagePath);

            PurgeDanglingPackages();

            foreach (var file in new DirectoryInfo(_storagePath).GetFiles())
            {
                if (file.Extension == RegistryConstants.FileExtensionVHD ||
                    file.Extension == RegistryConstants.FileExtensionSparseImage)
                {
                    // The filename is the SHA256 hash.
                    _availablePackages.Add(Path.GetFileNameWithoutExtension(file.Name));
                }
                else if (file.Extension == RegistryConstants.FileExtensionVHD + ".tmp" ||
                    file.Extension == RegistryConstants.FileExtensionSparseImage + ".tmp")
                {
                    // This is a partial download from a previous launch. Since we're restarting, this will be
                    // deleted the next time the package download is attempted, so we can just pre-emptively
                    // delete it now.
                    _logger.LogWarning($"Cleaning up temporary file in package storage: {file.FullName}");
                    File.Delete(file.FullName);
                }
                else if (file.Extension == ".tag")
                {
                    var info = JsonSerializer.Deserialize(
                        File.ReadAllText(file.FullName).Trim(),
                        PackageFsInternalJsonSerializerContext.Default.PackageStorageTag);
                    if (File.Exists(Path.Combine(_storagePath, info!.Hash + RegistryConstants.FileExtensionVHD)) ||
                        File.Exists(Path.Combine(_storagePath, info!.Hash + RegistryConstants.FileExtensionSparseImage)))
                    {
                        string tagHash = Hash.Sha256AsHexString(info!.Tag, Encoding.UTF8);
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
        }

        public void Dispose()
        {
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
            var normalizedPackageHash = packageDigest.Replace(":", "_", StringComparison.Ordinal);
            if (_availablePackages.Contains(normalizedPackageHash) &&
                File.Exists(Path.Combine(_storagePath, normalizedPackageHash + extension)))
            {
                // We do. Check if we need to update our tag data.
                if (_tagReferences.TryGetValue(tagHash, out var tagHashValue) &&
                    File.Exists(Path.Combine(_storagePath, tagHash + ".tag")))
                {
                    if (tagHashValue.Hash != normalizedPackageHash)
                    {
                        // Update the tag information on disk.
                        tagHashValue.Hash = normalizedPackageHash;
                        await File.WriteAllTextAsync(
                            Path.Combine(_storagePath, tagHash + ".tag"),
                            JsonSerializer.Serialize(
                                tagHashValue,
                                PackageFsInternalJsonSerializerContext.Default.PackageStorageTag)).ConfigureAwait(false);
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
                        Path.Combine(_storagePath, tagHash + ".tag"),
                        JsonSerializer.Serialize(
                            _tagReferences[tagHash],
                            PackageFsInternalJsonSerializerContext.Default.PackageStorageTag)).ConfigureAwait(false);
                }

                var resultPath = Path.Combine(_storagePath, normalizedPackageHash + extension);
                updatePollingResponse(
                    x =>
                    {
                        x.CompleteForPackage(resultPath, normalizedPackageHash);
                    },
                    resultPath);
                _logger.LogInformation($"skipping pull for {tag}, the on-disk copy is already up-to-date");
                return resultPath;
            }

            // If we already have this tag hash, delete it as it's now out of date.
            if (_tagReferences.ContainsKey(tagHash))
            {
                if (File.Exists(Path.Combine(_storagePath, tagHash + ".tag")))
                {
                    File.Delete(Path.Combine(_storagePath, tagHash + ".tag"));
                }
                _tagReferences.Remove(tagHash);
            }

            // Clean up any unused VHD packages (those that aren't referenced by anything), in case
            // we need to get more space on disk.
            PurgeDanglingPackages();

            _logger.LogInformation($"need to pull '{normalizedPackageHash}' as it does not exist on disk");

            // Copy data from the remote storage blob.
            var finalTargetPath = Path.Combine(_storagePath, normalizedPackageHash + extension);
            _logger.LogInformation($"Opening remote blob and storing at: {finalTargetPath}");
            using (var remoteStorageBlob = remoteStorageBlobFactory.Open())
            {
                updatePollingResponse(x =>
                {
                    x.PullingPackage(remoteStorageBlob.Length);
                }, null);

                _logger.LogInformation($"Cleaning up any temporary files...");
                var tempTargetPath = Path.Combine(_storagePath, normalizedPackageHash + extension + ".tmp");
                if (File.Exists(tempTargetPath))
                {
                    // Nuke it.
                    File.Delete(tempTargetPath);
                }
                if (File.Exists(finalTargetPath))
                {
                    // Nuke it.
                    File.Delete(finalTargetPath);
                }

                // Check if we have enough disk space.
                _logger.LogInformation($"Checking if we have enough disk space...");
                var driveInfo = new DriveInfo(_storagePath);
                var requiredBytes = remoteStorageBlob.Length + (2 * 1024 * 1024);
                if (driveInfo.AvailableFreeSpace < requiredBytes)
                {
                    throw new InvalidOperationException($"There is not enough space on the local system to store this package. The drive '{driveInfo}' has {driveInfo.AvailableFreeSpace / 1024 / 1024} MB of space, and we need {requiredBytes / 1024 / 1024} MB of space.");
                }

                // Copy it from the source, hashing while the copy takes place.
                string downloadedHash;
                _logger.LogInformation($"Opening temporary file path in write mode...");
                using (var target = new FileStream(tempTargetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    // Truncate to the size we need.
                    _logger.LogInformation($"Truncating it to the desired length...");
                    target.SetLength(remoteStorageBlob.Length);

                    // Allow other pull operations to start now.
                    _logger.LogInformation($"Releasing global lock to allow other operations to proceed...");
                    releaseGlobalPullLock();

                    // Perform the copy operation.
                    const int bufferSize = 8 * 1024 * 1024;
                    var buffer = new byte[bufferSize];
                    var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    _logger.LogInformation($"Copying the file content 8MB at a time...");
                    while (remoteStorageBlob.Position < remoteStorageBlob.Length)
                    {
                        var bytesRead = await remoteStorageBlob.ReadAsync(buffer, 0, bufferSize).ConfigureAwait(false);
                        await target.WriteAsync(buffer.AsMemory(0, bytesRead)).ConfigureAwait(false);
                        hasher.AppendData(buffer, 0, bytesRead);
                        updatePollingResponse(x =>
                        {
                            x.PullingPackageUpdatePosition(remoteStorageBlob.Position);
                        }, null);
                    }
                    var sha256Hash = Hash.HexString(hasher.GetCurrentHash());

                    // Get the hash and finalize.
                    _logger.LogInformation($"Finishing copy operation...");
                    downloadedHash = "sha256:" + sha256Hash;
                }

                // It doesn't match.
                if (packageDigest != downloadedHash)
                {
                    _logger.LogWarning("Downloaded package does not match the hash provided by the registry! This error will be ignored.");
                    /*
                    if (File.Exists(tempTargetPath))
                    {
                        File.Delete(tempTargetPath);
                    }
                    throw new InvalidOperationException($"Downloaded package does not match the hash provided by the registry. Please re-upload the package reference. Registry provided hash '{packageDigest}', computed hash was '{downloadedHash}'.");
                    */
                }

                // Move it into place.
                _logger.LogInformation($"Moving temporary file into final location...");
                File.Move(tempTargetPath, finalTargetPath);
                updatePollingResponse(
                    x =>
                    {
                        x.CompleteForPackage(finalTargetPath, normalizedPackageHash);
                    },
                    finalTargetPath);
            }

            // We now have this package on disk.
            _logger.LogInformation($"This package exists on disk.");
            _availablePackages.Add(normalizedPackageHash);
            _tagReferences[tagHash] = new PackageStorageTag
            {
                Hash = normalizedPackageHash,
                Tag = tag
            };
            await File.WriteAllTextAsync(
                Path.Combine(_storagePath, tagHash + ".tag"),
                JsonSerializer.Serialize(
                    _tagReferences[tagHash],
                    PackageFsInternalJsonSerializerContext.Default.PackageStorageTag)).ConfigureAwait(false);
            return finalTargetPath;
        }

        public Task VerifyAsync(
            bool isFixing,
            Action releaseGlobalPullLock,
            Action<Action<PollingResponse>> updatePollingResponse)
        {
            // Local storage doesn't need to verify anything since it doesn't fetch on-demand.
            updatePollingResponse(x =>
            {
                x.CompleteForVerifying();
            });
            return Task.CompletedTask;
        }

        private void PurgeDanglingPackages()
        {
            var availablePackages = new HashSet<string>();
            var tagReferences = new Dictionary<string, PackageStorageTag>();

            foreach (var file in new DirectoryInfo(_storagePath).GetFiles())
            {
                if (file.Extension == RegistryConstants.FileExtensionVHD ||
                    file.Extension == RegistryConstants.FileExtensionSparseImage)
                {
                    // The filename is the SHA256 hash.
                    availablePackages.Add(Path.GetFileNameWithoutExtension(file.Name));
                }
                else if (file.Extension == ".tag")
                {
                    var info = JsonSerializer.Deserialize(
                        File.ReadAllText(file.FullName).Trim(),
                        PackageFsInternalJsonSerializerContext.Default.PackageStorageTag);
                    if (File.Exists(Path.Combine(_storagePath, info!.Hash + RegistryConstants.FileExtensionVHD)) ||
                        File.Exists(Path.Combine(_storagePath, info!.Hash + RegistryConstants.FileExtensionSparseImage)))
                    {
                        string tagHash = Hash.Sha256AsHexString(info!.Tag, Encoding.UTF8);
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

            var packagesToDelete = new List<string>();
            foreach (var package in availablePackages)
            {
                if (!tagReferences.Any(x => x.Value.Hash == package))
                {
                    packagesToDelete.Add(package);
                }
            }

            availablePackages.RemoveWhere(x => packagesToDelete.Contains(x));
            foreach (var packageToDelete in packagesToDelete)
            {
                var extensions = new[] { RegistryConstants.FileExtensionVHD, RegistryConstants.FileExtensionSparseImage };
                foreach (var extension in extensions)
                {
                    _logger.LogInformation($"Automatically deleted dangling file: {Path.Combine(_storagePath, packageToDelete + extension)}");
                    File.Delete(Path.Combine(_storagePath, packageToDelete + extension));
                }
            }
        }
    }
}
