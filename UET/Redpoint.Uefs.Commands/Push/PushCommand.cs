namespace Redpoint.Uefs.Commands.Push
{
    using Docker.Registry.DotNet.Models;
    using Docker.Registry.DotNet.Registry;
    using Redpoint.ProgressMonitor;
    using Redpoint.Uefs.Commands.Hash;
    using Redpoint.Uefs.ContainerRegistry;
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO.MemoryMappedFiles;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Serialization.Metadata;
    using System.Threading.Tasks;

    public static class PushCommand
    {
        internal class Options
        {
            public Option<FileInfo> PackagePath = new Option<FileInfo>("--pkg", description: "The path of the package to push.") { IsRequired = true };
            public Option<string> PackageTag = new Option<string>("--tag", description: "The registry tag to push it to.") { IsRequired = true };
            public Option<string> PackageRef = new Option<string>("--ref", description: "If set, the passed value is used as the location to retrieve the data from, instead of storing the package in the registry itself. This can be used if your registry is not as fast as some other storage type (such as a network share), and you want to use the registry as the versioning system, but store the data elsewhere. The value for this parameter will typically be something like '\\\\COMPUTER\\Share\\mypackage.vhd' or '/Users/Shared/mypackage.sparseimage'.");
        }

        public static Command CreatePushCommand()
        {
            var options = new Options();
            var command = new Command("push", "Push a UEFS package to a container registry.");
            command.AddAllOptions(options);
            command.AddCommonHandler<PushCommandInstance>(options);
            return command;
        }

        private class PushCommandInstance : ICommandInstance
        {
            private readonly IFileHasher _fileHasher;
            private readonly IMonitorFactory _monitorFactory;
            private readonly IProgressFactory _progressFactory;
            private readonly Options _options;

            public PushCommandInstance(
                IFileHasher fileHasher,
                IMonitorFactory monitorFactory,
                IProgressFactory progressFactory,
                Options options)
            {
                _fileHasher = fileHasher;
                _monitorFactory = monitorFactory;
                _progressFactory = progressFactory;
                _options = options;
            }

            public async Task<int> ExecuteAsync(InvocationContext context)
            {
                var packagePath = context.ParseResult.GetValueForOption(_options.PackagePath);
                var packageTag = context.ParseResult.GetValueForOption(_options.PackageTag);
                var packageRef = context.ParseResult.GetValueForOption(_options.PackageRef);
                if (packagePath == null || !packagePath.Exists)
                {
                    Console.Error.WriteLine("error: input package must exist.");
                    return 1;
                }
                if (string.IsNullOrWhiteSpace(packageTag))
                {
                    Console.Error.WriteLine("error: you must specify the tag to push to.");
                    return 1;
                }
                var tag = RegistryTagRegex.Regex.Match(packageTag);
                if (!tag.Success)
                {
                    Console.Error.WriteLine("error: invalid tag format");
                    return 1;
                }

                if (!packagePath.Name.EndsWith(RegistryConstants.FileExtensionVHD) &&
                    !packagePath.Name.EndsWith(RegistryConstants.FileExtensionSparseImage))
                {
                    Console.Error.WriteLine($"error: package name should end in {RegistryConstants.FileExtensionVHD} or {RegistryConstants.FileExtensionSparseImage}");
                    return 1;
                }

                var host = tag.Groups["host"].Value;
                var path = tag.Groups["path"].Value;
                var label = tag.Groups["label"].Value;

                // Compute the digest for the package first. We need it in both modes.
                string sha256 = await _fileHasher.ComputeHashAsync(packagePath);

                // Connect to the registry.
                var clientCredential = RegistryClientFactory.GetRegistryCredential(host);
                if (clientCredential == null)
                {
                    Console.WriteLine("error: unable to get registry credentials from WinStore");
                    return 1;
                }
                var client = RegistryClientFactory.GetRegistryClient(host, clientCredential);
                using (client)
                {
                    string configDigest;
                    long configLength;
                    string pkgMediaType;
                    string pkgDigest;
                    long pkgLength;

                    // Upload the empty config blob. This is required by at least GitLab Registry, even though
                    // the config is empty.
                    {
                        Console.WriteLine("uploading config to registry...");
                        var info = JsonSerializer.Serialize(new RegistryImageConfig { }, UefsCommandJsonSerializerContext.Default.RegistryImageConfig);
                        using (var hasher = SHA256.Create())
                        {
                            configDigest = "sha256:" + BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(info))).Replace("-", "").ToLowerInvariant();
                        }
                        if (!await client.Blobs.IsExistBlobAsync(path, configDigest))
                        {
                            var upload = await client.BlobUploads.StartUploadBlobAsync(path);
                            using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(info)))
                            {
                                await client.BlobUploads.CompleteBlobUploadAsync(upload, configDigest, memory);
                            }
                        }
                        configLength = Encoding.UTF8.GetBytes(info).Length;
                    }

                    // If we are uploading the package itself...
                    if (string.IsNullOrWhiteSpace(packageRef))
                    {
                        Console.WriteLine("uploading package to registry...");
                        var packageInfo = new FileInfo(packagePath.FullName);
                        using (var mmap = MemoryMappedFile.CreateFromFile(packagePath.FullName, FileMode.Open, null, 0))
                        {
                            if (await client.Blobs.IsExistBlobAsync(path, sha256))
                            {
                                Console.WriteLine("already exists; skipping blob upload");
                            }
                            else
                            {
                                var chunkedStreamProgress = new ChunkedStreamProgress();
                                var progress = _progressFactory.CreateProgressForStream(chunkedStreamProgress, packageInfo.Length);

                                var cts = new CancellationTokenSource();
                                var monitorTask = Task.Run(async () =>
                                {
                                    var monitor = _monitorFactory.CreateByteBasedMonitor();
                                    await monitor.MonitorAsync(
                                        progress,
                                        SystemConsole.ConsoleInformation,
                                        SystemConsole.WriteProgressToConsole,
                                        cts.Token);
                                });

                                try
                                {
                                    var upload = await client.BlobUploads.StartUploadBlobAsync(path);
                                    var startPosition = long.Parse(upload.Range.Split("-")[1]);

                                    const long chunkSize = 1024 * 1024 * 1024;

                                    for (long from = startPosition; from < packageInfo.Length; from += chunkSize)
                                    {
                                        var to = Math.Min(packageInfo.Length, from + chunkSize);
                                        using (var view = mmap.CreateViewStream(from, to - from))
                                        {
                                            chunkedStreamProgress.ChunkOffset = from;
                                            chunkedStreamProgress.ChunkStream = view;
                                            upload = await client.BlobUploads.UploadBlobChunkAsync(
                                                upload,
                                                view,
                                                from,
                                                to);
                                        }
                                    }
                                    await client.BlobUploads.CompleteBlobUploadAsync(upload, sha256);
                                }
                                finally
                                {
                                    await SystemConsole.CancelAndWaitForConsoleMonitoringTaskAsync(monitorTask, cts);
                                }
                            }
                        }

                        if (packagePath.FullName.EndsWith(RegistryConstants.FileExtensionVHD))
                        {
                            pkgMediaType = RegistryConstants.MediaTypePackageVHD;
                        }
                        else if (packagePath.FullName.EndsWith(RegistryConstants.FileExtensionSparseImage))
                        {
                            pkgMediaType = RegistryConstants.MediaTypePackageSparseImage;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }

                        pkgLength = packageInfo.Length;
                        pkgDigest = sha256;
                    }
                    // Otherwise if we are uploading just a location reference...
                    else
                    {
                        Console.WriteLine("uploading reference to registry...");
                        (pkgDigest, pkgLength) = await UploadTiny(
                            client,
                            path,
                            new RegistryReferenceInfo
                            {
                                Location = packageRef,
                                Digest = sha256,
                            },
                            UefsCommandJsonSerializerContext.Default.RegistryReferenceInfo);

                        if (packagePath.FullName.EndsWith(RegistryConstants.FileExtensionVHD))
                        {
                            pkgMediaType = RegistryConstants.MediaTypePackageReferenceVHD;
                        }
                        else if (packagePath.FullName.EndsWith(RegistryConstants.FileExtensionSparseImage))
                        {
                            pkgMediaType = RegistryConstants.MediaTypePackageReferenceSparseImage;
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }

                    // Try to get the existing manifest list so we can add platforms without
                    // replacing content for other platforms.
                    var existingManifests = new List<Manifest>();
                    try
                    {
                        var existingManifest = await client.Manifest.GetManifestAsync(
                            path,
                            label);
                        if (existingManifest?.Manifest is ImageManifest2_2 existingManifestSchema2)
                        {
                            // This is an older upload, which didn't use the manifest list system.
                            if (existingManifestSchema2.Layers[0].MediaType != RegistryConstants.MediaTypeLegacyPackageReference)
                            {
                                throw new InvalidOperationException($"Unexpected media type in legacy package: {existingManifestSchema2.Layers[0].MediaType}");
                            }

                            // Immediately re-upload this manifest as a blob so we can reference it.
                            existingManifestSchema2.Layers[0].MediaType = RegistryConstants.MediaTypePackageReferenceVHD;
                            Console.WriteLine("updating existing legacy manifest");
                            var (legacyDigest, legacyLength) = await UploadIntermediateManifest(
                                client,
                                path,
                                label,
                                RegistryConstants.PlatformWindows,
                                existingManifestSchema2);

                            // Add the manifest.
                            existingManifests.Add(new Manifest
                            {
                                MediaType = RegistryConstants.MediaTypeManifestV2,
                                Size = legacyLength,
                                Digest = legacyDigest,
                                Platform = new Platform
                                {
                                    Os = RegistryConstants.PlatformWindows
                                }
                            });
                        }
                        else if (existingManifest?.Manifest is ManifestList existingManifestList)
                        {
                            // Newer upload, just import manifest lists directly.
                            existingManifests.AddRange(existingManifestList.Manifests);
                        }
                        else
                        {
                            throw new NotSupportedException("unknown manifest format!");
                        }
                    }
                    catch (RegistryApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        // This is OK, it means the manifest doesn't exist at all.
                    }

                    // Determine the platform for the new manifest (for the list entry).
                    var platform = RegistryConstants.PlatformWindows;
                    if (pkgMediaType == RegistryConstants.MediaTypePackageSparseImage ||
                        pkgMediaType == RegistryConstants.MediaTypePackageReferenceSparseImage)
                    {
                        platform = RegistryConstants.PlatformMacOS;
                    }

                    // Upload the manifest for this tag.
                    var (newManifestDigest, newManifestLength) = await UploadIntermediateManifest(
                        client,
                        path,
                        label,
                        platform,
                        new ImageManifest2_2
                        {
                            SchemaVersion = 2,
                            MediaType = RegistryConstants.MediaTypeManifestV2,
                            Config = new Config
                            {
                                MediaType = RegistryConstants.MediaTypeManifestImageV1,
                                Size = configLength,
                                Digest = configDigest,
                            },
                            Layers = new ManifestLayer[]
                            {
                                new ManifestLayer
                                {
                                    MediaType = pkgMediaType,
                                    Size = pkgLength,
                                    Digest = pkgDigest,
                                }
                            }
                        });

                    // Generate the manifest list itself.
                    var manifestList = new ManifestList
                    {
                        SchemaVersion = 2,
                        MediaType = RegistryConstants.MediaTypeManifestListV2,
                        Manifests = existingManifests
                            // Exclude any existing entries for this platform.
                            .Where(x => x.Platform.Os != platform)
                            // Attach our new manifest for this platform.
                            .Concat(new[]
                            {
                                new Manifest
                                {
                                    MediaType = RegistryConstants.MediaTypeManifestV2,
                                    Size = newManifestLength,
                                    Digest = newManifestDigest,
                                    Platform = new Platform
                                    {
                                        Os = platform
                                    }
                                }
                            })
                            .ToArray()
                    };
                    foreach (var manifest in manifestList.Manifests)
                    {
                        Console.WriteLine($"manifest entry for {manifest.Platform.Os} is {manifest.Digest}");
                    }

                    // Upload the manifest for this tag.
                    await client.Manifest.PutManifestAsync(path, label, manifestList);
                }

                Console.WriteLine("push complete");
                return 0;
            }

            private async static Task<(string digest, int length)> UploadTiny<T>(
                IRegistryClient client,
                string path,
                T data,
                JsonTypeInfo<T> typeInfo)
            {
                string pkgDigest;
                var info = JsonSerializer.Serialize(data, typeInfo);
                using (var hasher = SHA256.Create())
                {
                    pkgDigest = "sha256:" + BitConverter.ToString(hasher.ComputeHash(Encoding.UTF8.GetBytes(info))).Replace("-", "").ToLowerInvariant();
                }
                if (!await client.Blobs.IsExistBlobAsync(path, pkgDigest))
                {
                    var upload = await client.BlobUploads.StartUploadBlobAsync(path);
                    using (var memory = new MemoryStream(Encoding.UTF8.GetBytes(info)))
                    {
                        await client.BlobUploads.CompleteBlobUploadAsync(upload, pkgDigest, memory);
                    }
                    Console.WriteLine($"tiny blob uploaded: {pkgDigest} ({path})");
                }
                else
                {
                    Console.WriteLine($"tiny blob already exists: {pkgDigest} ({path})");
                }
                return (pkgDigest, Encoding.UTF8.GetBytes(info).Length);
            }

            private async static Task<(string digest, int length)> UploadIntermediateManifest(
                IRegistryClient client,
                string path,
                string label,
                string platform,
                ImageManifest manifest)
            {
                var platformedLabel = $"{label}-uefsplatform-{platform}";
                var response = await client.Manifest.PutManifestAsync(path, platformedLabel, manifest);
                Console.WriteLine($"intermediate manifest uploaded for: {platform}: {response.DockerContentDigest} ({platformedLabel})");
                return (response.DockerContentDigest, int.Parse(response.ContentLength));
            }
        }
    }
}
