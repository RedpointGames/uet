namespace Redpoint.Uefs.Daemon.Transactional.Executors
{
    using Docker.Registry.DotNet.Models;
    using Redpoint.Uefs.Daemon.PackageFs;
    using System.Text.Json;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Security.Cryptography;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Daemon.RemoteStorage;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Hashing;

    internal sealed class PullPackageTagTransactionExecutor : ITransactionExecutor<PullPackageTagTransactionRequest, PullPackageTagTransactionResult>
    {
        private readonly IRemoteStorage<ManifestLayer> _registryRemoteStorage;
        private readonly IRemoteStorage<RegistryReferenceInfo> _referenceRemoteStorage;

        public PullPackageTagTransactionExecutor(
            IRemoteStorage<ManifestLayer> registryRemoteStorage,
            IRemoteStorage<RegistryReferenceInfo> referenceRemoteStorage)
        {
            _registryRemoteStorage = registryRemoteStorage;
            _referenceRemoteStorage = referenceRemoteStorage;
        }

        public async Task<PullPackageTagTransactionResult> ExecuteTransactionAsync(
            ITransactionContext<PullPackageTagTransactionResult> context,
            PullPackageTagTransactionRequest transaction,
            CancellationToken cancellationToken)
        {
            context.UpdatePollingResponse(x =>
            {
                x.Init(PollingResponseType.Package);
                x.Checking();
            });

            // Connect to the registry to check the current digest.
            var tagComponents = RegistryTagRegex.Regex.Match(transaction.Tag);
            if (!tagComponents.Success)
            {
                throw new InvalidOperationException("Invalid package URL");
            }

            var host = tagComponents.Groups["host"].Value;
            var path = tagComponents.Groups["path"].Value;
            var label = tagComponents.Groups["label"].Value;

            string tagHash = Hash.Sha256AsHexString(transaction.Tag, Encoding.UTF8);

            var client = RegistryClientFactory.GetRegistryClient(host, new ContainerRegistry.RegistryCredential
            {
                Username = transaction.Credential.Username,
                Password = transaction.Credential.Password,
            });
            if (client == null)
            {
                throw new InvalidOperationException("The daemon is unable to connect to this package registry");
            }
            ImageManifest2_2? registryManifest = null;
            using (client)
            {
                // Download the manifest.
                var manifest = await client.Manifest.GetManifestAsync(path, label).ConfigureAwait(false);

                // Try to get the manifest list, and from there get the registry manifest
                // for the current platform. We also have to handle legacy manifests.
                if (manifest?.Manifest is ImageManifest2_2 legacyManifest)
                {
                    if (legacyManifest.Layers[0].MediaType != RegistryConstants.MediaTypeLegacyPackageReference)
                    {
                        throw new InvalidOperationException("Unexpected media type for legacy package reference!");
                    }

                    if (OperatingSystem.IsWindows())
                    {
                        registryManifest = legacyManifest;
                        registryManifest.Layers[0].MediaType = RegistryConstants.MediaTypePackageReferenceVHD;
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        throw new InvalidOperationException("No package content for macOS for this tag.");
                    }
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }
                }
                else if (manifest?.Manifest is ManifestList manifestList)
                {
                    // Figure out the platform we want.
                    string targetPlatform;
                    if (OperatingSystem.IsWindows())
                    {
                        targetPlatform = RegistryConstants.PlatformWindows;
                    }
                    else if (OperatingSystem.IsMacOS())
                    {
                        targetPlatform = RegistryConstants.PlatformMacOS;
                    }
                    else
                    {
                        throw new PlatformNotSupportedException();
                    }

                    // Figure out the manifest we want based on the platform.
                    var selectedManifest = manifestList.Manifests.FirstOrDefault(x => x.Platform.Os == targetPlatform);
                    if (selectedManifest == null)
                    {
                        throw new InvalidOperationException("No package content for this platform for this tag.");
                    }

                    // Download the actual manifest file from blob storage.
                    registryManifest = (await client.Manifest.GetManifestAsync(path, selectedManifest.Digest, true).ConfigureAwait(false))?.Manifest as ImageManifest2_2;
                }

                if (registryManifest == null || !registryManifest.Layers.Any())
                {
                    throw new InvalidOperationException("No such package was found on the registry");
                }

                var packageManifest = registryManifest.Layers[0];
                string packageDigest;
                RegistryReferenceInfo? packageReferenceInfo = null;

                // Figure out the package extension.
                var extension = RegistryConstants.FileExtensionVHD;
                if (packageManifest.MediaType == RegistryConstants.MediaTypePackageSparseImage ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceSparseImage)
                {
                    extension = RegistryConstants.FileExtensionSparseImage;
                }

                // If the package is stored in the registry...
                if (packageManifest.MediaType == RegistryConstants.MediaTypePackageVHD ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageSparseImage)
                {
                    // The package digest is directly the value in the manifest.
                    packageDigest = packageManifest.Digest;
                }
                // If this is a reference to another location...
                else if (packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceVHD ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceSparseImage)
                {
                    // We have to pull this content layer to get the reference data.
                    var getResponse = await client.Blobs.GetBlobAsync(path, packageManifest.Digest).ConfigureAwait(false);
                    using (var reader = new StreamReader(getResponse.Stream))
                    {
                        packageReferenceInfo = JsonSerializer.Deserialize(
                            await reader.ReadToEndAsync().ConfigureAwait(false),
                            UefsRegistryJsonSerializerContext.Default.RegistryReferenceInfo);
                    }
                    if (packageReferenceInfo == null ||
                        string.IsNullOrWhiteSpace(packageReferenceInfo.Location) ||
                        string.IsNullOrWhiteSpace(packageReferenceInfo.Digest))
                    {
                        throw new InvalidOperationException("Package reference data on the registry is invalid");
                    }
                    packageDigest = packageReferenceInfo.Digest;
                }
                // Otherwise, fail.
                else
                {
                    throw new InvalidOperationException("The package manifest does not reference a valid UEFS package");
                }

                string packagePath;
                if (packageManifest.MediaType == RegistryConstants.MediaTypePackageVHD ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageSparseImage)
                {
                    using (var blobFactory = _registryRemoteStorage.GetFactory(packageManifest))
                    {
                        var @lock = await context.ObtainLockAsync("PackageStorage", cancellationToken).ConfigureAwait(false);
                        var didReleaseSemaphore = false;
                        try
                        {
                            context.UpdatePollingResponse(x =>
                            {
                                x.Starting();
                            });

                            packagePath = await transaction.PackageFs.PullAsync(
                                blobFactory,
                                _registryRemoteStorage.Type,
                                packageManifest,
                                PackageFsJsonSerializerContext.Default.ManifestLayer,
                                packageDigest,
                                extension,
                                tagHash,
                                transaction.Tag,
                                () =>
                                {
                                    if (!didReleaseSemaphore)
                                    {
                                        didReleaseSemaphore = true;
                                        @lock.Dispose();
                                    }
                                },
                                (callback, packagePath) =>
                                {
                                    context.UpdatePollingResponse(
                                        callback,
                                        packagePath != null ? new PullPackageTagTransactionResult { PackagePath = new FileInfo(packagePath) } : null);
                                }).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (!didReleaseSemaphore)
                            {
                                didReleaseSemaphore = true;
                                @lock.Dispose();
                            }
                        }
                    }
                }
                else if (packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceVHD ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceSparseImage)
                {
                    using (var blobFactory = _referenceRemoteStorage.GetFactory(packageReferenceInfo!))
                    {
                        var @lock = await context.ObtainLockAsync("PackageStorage", cancellationToken).ConfigureAwait(false);
                        var didReleaseSemaphore = false;
                        try
                        {
                            context.UpdatePollingResponse(x =>
                            {
                                x.Starting();
                            });

                            packagePath = await transaction.PackageFs.PullAsync(
                                blobFactory,
                                _referenceRemoteStorage.Type,
                                packageReferenceInfo!,
                                UefsRegistryJsonSerializerContext.Default.RegistryReferenceInfo,
                                packageDigest,
                                extension,
                                tagHash,
                                transaction.Tag,
                                () =>
                                {
                                    if (!didReleaseSemaphore)
                                    {
                                        didReleaseSemaphore = true;
                                        @lock.Dispose();
                                    }
                                },
                                (callback, packagePath) =>
                                {
                                    context.UpdatePollingResponse(
                                        callback,
                                        packagePath != null ? new PullPackageTagTransactionResult { PackagePath = new FileInfo(packagePath) } : null);
                                }).ConfigureAwait(false);
                        }
                        finally
                        {
                            if (!didReleaseSemaphore)
                            {
                                didReleaseSemaphore = true;
                                @lock.Dispose();
                            }
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException("The package manifest does not have a known media type.");
                }

                return new PullPackageTagTransactionResult
                {
                    PackagePath = new FileInfo(packagePath),
                };
            }
        }
    }
}
