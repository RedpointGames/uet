namespace Redpoint.Uet.Uefs.InProcess
{
    using Docker.Registry.DotNet.Models;
    using Microsoft.Extensions.Logging;
    using Redpoint.Hashing;
    using Redpoint.ProgressMonitor.Utils;
    using Redpoint.Uefs.ContainerRegistry;
    using Redpoint.Uefs.Protocol;
    using Redpoint.Uet.CommonPaths;
    using Redpoint.Uet.Workspace.Reservation;
    using System.Text;
    using System.Text.Json;

    public class DefaultUefsInProcessManager : IUefsInProcessManager
    {
        private readonly ILogger<DefaultUefsInProcessManager> _logger;
        private readonly ISimpleDownloadProgress _simpleDownloadProgress;

        public DefaultUefsInProcessManager(
            ILogger<DefaultUefsInProcessManager> logger,
            ISimpleDownloadProgress simpleDownloadProgress)
        {
            _logger = logger;
            _simpleDownloadProgress = simpleDownloadProgress;
        }

        public async Task<string> PullTagToPackagePathAsync(MountPackageTagRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            // Determine the package storage path (this isn't done via reservation, because we want concurrent access).
            UetPaths.InitUefsRootPath(x => _logger.LogInformation(x));
            var packageStoragePath = Path.Combine(UetPaths.UefsRootPath, "inprocpkgs");
            Directory.CreateDirectory(packageStoragePath);

            // Download or copy the package so it exists locally.
            var packagePath = await PullTagAndResolveToPackagePath(
                request,
                packageStoragePath,
                cancellationToken);

            // Return the package path.
            return packagePath;
        }

        private async Task<string> PullTagAndResolveToPackagePath(
            MountPackageTagRequest request,
            string packageStoragePath,
            CancellationToken cancellationToken)
        {
            // Connect to the registry to check the current digest.
            var tagComponents = RegistryTagRegex.Regex.Match(request.Tag);
            if (!tagComponents.Success)
            {
                throw new InvalidOperationException("Invalid package URL");
            }

            var host = tagComponents.Groups["host"].Value;
            var path = tagComponents.Groups["path"].Value;
            var label = tagComponents.Groups["label"].Value;

            _logger.LogInformation($"Pulling from {host}/{path}:{label}");

            string tagHash = Hash.Sha256AsHexString(request.Tag, Encoding.UTF8);

            _logger.LogInformation($"Getting container registry client...");
            var client = RegistryClientFactory.GetRegistryClient(host, new Redpoint.Uefs.ContainerRegistry.RegistryCredential
            {
                Username = request.Credential.Username,
                Password = request.Credential.Password,
            });
            if (client == null)
            {
                throw new InvalidOperationException("Unable to connect to the container registry!");
            }
            ImageManifest2_2? registryManifest = null;
            using (client)
            {
                // Download the manifest.
                _logger.LogInformation($"Downloading the manifest for the selected tag...");
                var manifest = await client.Manifest.GetManifestAsync(path, label, cancellationToken: cancellationToken).ConfigureAwait(false);

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
                    _logger.LogInformation($"Figuring out the desired manifest based on the current platform...");
                    var selectedManifest = manifestList.Manifests.FirstOrDefault(x => x.Platform.Os == targetPlatform);
                    if (selectedManifest == null)
                    {
                        throw new InvalidOperationException("No package content for this platform for this tag.");
                    }

                    // Download the actual manifest file from blob storage.
                    _logger.LogInformation($"Downloading the manifest file with digest {selectedManifest.Digest}...");
                    registryManifest = (await client.Manifest.GetManifestAsync(path, selectedManifest.Digest, true, cancellationToken).ConfigureAwait(false))?.Manifest as ImageManifest2_2;
                }

                if (registryManifest == null || registryManifest.Layers.Length == 0)
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
                _logger.LogInformation($"Downloading the package file with extension '{extension}'.");

                // If the package is stored in the registry...
                _logger.LogInformation($"Resolving the package manifest to a file we can download...");
                if (packageManifest.MediaType == RegistryConstants.MediaTypePackageVHD ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageSparseImage)
                {
                    // The package digest is directly the value in the manifest.
                    packageDigest = packageManifest.Digest;
                }
                // If this is a reference to another location...
                else if (
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceVHD ||
                    packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceSparseImage)
                {
                    // We have to pull this content layer to get the reference data.
                    var getResponse = await client.Blobs.GetBlobAsync(path, packageManifest.Digest, cancellationToken).ConfigureAwait(false);
                    using (var reader = new StreamReader(getResponse.Stream))
                    {
                        packageReferenceInfo = JsonSerializer.Deserialize(
                            await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false),
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

                // Check if the package is already downloaded.
                var desiredPackagePath = Path.Combine(
                    packageStoragePath,
                    $"{packageDigest}{extension}");
                if (File.Exists(desiredPackagePath))
                {
                    return desiredPackagePath;
                }

                // Copy the package data to a temporary file (that can be cleaned up if this download gets interrupted).
                var temporaryPackagePath = Path.Combine(
                    packageStoragePath,
                    $"{packageDigest}{extension}.tmp.{Environment.ProcessId}");
                try
                {
                    if (packageManifest.MediaType == RegistryConstants.MediaTypePackageVHD ||
                        packageManifest.MediaType == RegistryConstants.MediaTypePackageSparseImage)
                    {
                        var getResponse = await client.Blobs.GetBlobAsync(path, packageManifest.Digest, cancellationToken).ConfigureAwait(false);

                        _logger.LogInformation($"Downloading package file from registry with digest '{packageManifest.Digest}' ({packageManifest.Size} bytes) to '{temporaryPackagePath}'...");
                        using (var fileStream = new FileStream(temporaryPackagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                        {
                            await _simpleDownloadProgress.CopyToStreamAsync(
                                getResponse.Stream,
                                packageManifest.Size,
                                async stream => await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false),
                                cancellationToken).ConfigureAwait(false);
                        }
                    }
                    else if (
                        packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceVHD ||
                        packageManifest.MediaType == RegistryConstants.MediaTypePackageReferenceSparseImage)
                    {
                        using (var sourceFileStream = new FileStream(packageReferenceInfo!.Location, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            _logger.LogInformation($"Copying referenced package file from '{packageReferenceInfo.Location}' ({sourceFileStream.Length} bytes) to '{temporaryPackagePath}'...");
                            using (var fileStream = new FileStream(temporaryPackagePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                            {
                                await _simpleDownloadProgress.CopyToStreamAsync(
                                    sourceFileStream,
                                    sourceFileStream.Length,
                                    async stream => await stream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false),
                                    cancellationToken).ConfigureAwait(false);
                            }
                        }
                    }
                    else
                    {
                        throw new InvalidOperationException("The package manifest does not have a known media type.");
                    }

                    // Move the temporary file in-place over the desired path.
                    if (!File.Exists(desiredPackagePath))
                    {
                        try
                        {
                            File.Move(temporaryPackagePath, desiredPackagePath, false);
                        }
                        catch (Exception) when (File.Exists(desiredPackagePath))
                        {
                            // Ignored; multiple processes tried to move the temporary package into the desired path at the same time.
                        }
                    }
                    return desiredPackagePath;
                }
                finally
                {
                    if (File.Exists(temporaryPackagePath))
                    {
                        File.Delete(temporaryPackagePath);
                    }
                }
            }
        }
    }
}