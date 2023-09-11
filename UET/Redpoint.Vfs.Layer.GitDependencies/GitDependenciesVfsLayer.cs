namespace Redpoint.Vfs.Layer.GitDependencies
{
    using Microsoft.Extensions.Logging;
    using Redpoint.Vfs.Abstractions;
    using Redpoint.Vfs.Layer.Git;
    using Redpoint.Vfs.LocalIo;
    using System.IO.Compression;
    using System.Xml;

    internal sealed class GitDependenciesVfsLayer : IGitDependenciesVfsLayer
    {
        private readonly ILogger<GitDependenciesVfsLayer> _logger;
        private readonly ILocalIoVfsFileFactory _localIoVfsFileFactory;
        private readonly string _cachePath;
        private readonly IGitVfsLayer _nextLayer;

        private readonly Dictionary<string, VfsEntry> _treeProjection;
        private readonly Dictionary<string, VfsEntry[]> _treeExpansionProjectionSorted;
        private readonly Dictionary<string, VfsEntry> _fileProjection;
        private readonly Dictionary<string, DependencyFile> _files;
        private readonly Dictionary<string, DependencyBlob> _blobs;
        private readonly Dictionary<string, DependencyPack> _packs;
        private readonly Concurrency.Semaphore _globalSemaphore;

        public GitDependenciesVfsLayer(
            ILogger<GitDependenciesVfsLayer> logger,
            ILocalIoVfsFileFactory localIoVfsFileFactory,
            string cachePath,
            IGitVfsLayer nextLayer)
        {
            _logger = logger;
            _localIoVfsFileFactory = localIoVfsFileFactory;
            _cachePath = cachePath;
            _nextLayer = nextLayer;

            _treeProjection = new Dictionary<string, VfsEntry>();
            _treeExpansionProjectionSorted = new Dictionary<string, VfsEntry[]>();
            _fileProjection = new Dictionary<string, VfsEntry>();
            _files = new Dictionary<string, DependencyFile>(StringComparer.InvariantCultureIgnoreCase);
            _blobs = new Dictionary<string, DependencyBlob>();
            _packs = new Dictionary<string, DependencyPack>();
            _globalSemaphore = new Concurrency.Semaphore(1);
        }

        public async Task InitAsync(CancellationToken cancellationToken)
        {
            // Make sure it's initialized.
            await _nextLayer.InitAsync(cancellationToken).ConfigureAwait(false);

            var treeExpansionProjection = new Dictionary<string, Dictionary<string, VfsEntry>>();
            foreach (var depsFile in _nextLayer.Files.Where(x => x.Key.EndsWith(".gitdeps.xml", StringComparison.InvariantCultureIgnoreCase)))
            {
                VfsEntry? metadata = null;
                using (var handle = _nextLayer.OpenFile(depsFile.Key, FileMode.Open, FileAccess.Read, FileShare.Read, ref metadata))
                {
                    var buffer = new byte[handle!.VfsFile.Length];
                    if (handle.VfsFile.ReadFile(buffer, out uint bytesRead, 0) != 0x0 ||
                        bytesRead != buffer.Length)
                    {
                        throw new InvalidOperationException("Failed to read .gitdeps.xml file properly!");
                    }

                    var document = new XmlDocument();
                    using (var stream = new MemoryStream(buffer))
                    {
                        document.Load(stream);
                    }

                    foreach (var pack in document.SelectSingleNode("//Packs")!.ChildNodes.OfType<XmlElement>())
                    {
                        _ = long.TryParse(pack.GetAttribute("Size"), out long size);
                        _ = long.TryParse(pack.GetAttribute("CompressedSize"), out long compressedSize);

                        _packs.Add(
                            pack.GetAttribute("Hash"),
                            new DependencyPack
                            {
                                Hash = pack.GetAttribute("Hash"),
                                Size = size,
                                CompressedSize = compressedSize,
                                RemotePath = pack.GetAttribute("RemotePath")
                            });
                    }

                    foreach (var blob in document.SelectSingleNode("//Blobs")!.ChildNodes.OfType<XmlElement>())
                    {
                        _ = long.TryParse(blob.GetAttribute("Size"), out long size);
                        _ = long.TryParse(blob.GetAttribute("PackOffset"), out long packOffset);

                        _blobs.Add(
                            blob.GetAttribute("Hash"),
                            new DependencyBlob
                            {
                                Hash = blob.GetAttribute("Hash"),
                                Size = size,
                                PackHash = blob.GetAttribute("PackHash"),
                                PackOffset = packOffset,
                            });
                    }

                    foreach (var file in document.SelectSingleNode("//Files")!.ChildNodes.OfType<XmlElement>())
                    {
                        _files.Add(
                            file.GetAttribute("Name").Replace('/', '\\'),
                            new DependencyFile
                            {
                                Name = file.GetAttribute("Name"),
                                Hash = file.GetAttribute("Hash"),
                                IsExecutable = (file.GetAttribute("IsExecutable") ?? "false").Equals("true", StringComparison.OrdinalIgnoreCase),
                            });
                        var fileProjection = new VfsEntry
                        {
                            CreationTime = _nextLayer.Created,
                            LastWriteTime = _nextLayer.Created,
                            LastAccessTime = _nextLayer.Created,
                            ChangeTime = _nextLayer.Created,
                            Attributes = FileAttributes.Archive,
                            Name = Path.GetFileName(file.GetAttribute("Name")),
                            Size = _blobs[file.GetAttribute("Hash")].Size,
                        };
                        _fileProjection.Add(
                            file.GetAttribute("Name").ToLowerInvariant().Replace('/', '\\'),
                            fileProjection);
                        var components = file.GetAttribute("Name").Split('/');
                        for (var i = 0; i < components.Length - 1; i++)
                        {
                            var parentPath = string.Join('\\', components.Take(i + 1));
                            if (!_treeProjection.ContainsKey(parentPath.ToLowerInvariant()))
                            {
                                _treeProjection.Add(parentPath.ToLowerInvariant(), new VfsEntry
                                {
                                    CreationTime = _nextLayer.Created,
                                    LastWriteTime = _nextLayer.Created,
                                    LastAccessTime = _nextLayer.Created,
                                    ChangeTime = _nextLayer.Created,
                                    Attributes = FileAttributes.Directory,
                                    Name = Path.GetFileName(parentPath),
                                    Size = 0,
                                });
                            }
                            if (i != 0)
                            {
                                var parentParentPath = string.Join('\\', components.Take(i));
                                if (!treeExpansionProjection.ContainsKey(parentParentPath.ToLowerInvariant()))
                                {
                                    treeExpansionProjection.Add(parentParentPath.ToLowerInvariant(), new Dictionary<string, VfsEntry>());
                                }
                                if (!treeExpansionProjection[parentParentPath.ToLowerInvariant()].ContainsKey(Path.GetFileName(parentPath).ToLowerInvariant()))
                                {
                                    treeExpansionProjection[parentParentPath.ToLowerInvariant()].Add(Path.GetFileName(parentPath).ToLowerInvariant(), _treeProjection[parentPath.ToLowerInvariant()]);
                                }
                            }
                        }
                        var fileParentPath = string.Join('\\', components.Take(components.Length - 1));
                        if (!treeExpansionProjection.ContainsKey(fileParentPath.ToLowerInvariant()))
                        {
                            treeExpansionProjection.Add(fileParentPath.ToLowerInvariant(), new Dictionary<string, VfsEntry>());
                        }
                        if (!treeExpansionProjection[fileParentPath.ToLowerInvariant()].ContainsKey(fileProjection.Name.ToLowerInvariant()))
                        {
                            treeExpansionProjection[fileParentPath.ToLowerInvariant()].Add(fileProjection.Name.ToLowerInvariant(), fileProjection);
                        }
                    }
                }
            }
            var comparer = new FileSystemNameComparer();
            foreach (var kv in treeExpansionProjection)
            {
                _treeExpansionProjectionSorted.Add(kv.Key, kv.Value.Values.OrderBy(x => x.Name, comparer).ToArray());
            }

            Console.WriteLine($"Initialized dependencies layer with:");
            Console.WriteLine($"  File projections: {_fileProjection.Count}");
            Console.WriteLine($"  Tree projections: {_treeProjection.Count}");
            Console.WriteLine($"  Tree expansion projections: {_treeExpansionProjectionSorted.Count}");
            Console.WriteLine($"  Files: {_files.Count}");
            Console.WriteLine($"  Blobs: {_blobs.Count}");
            Console.WriteLine($"  Packs: {_packs.Count}");
        }

        public void Dispose()
        {
            _nextLayer?.Dispose();
        }

        public bool ReadOnly => true;

        private IEnumerable<VfsEntry> ListAggregated(string pathKey, string path)
        {
            // @note: This used to do emittedFiles on _treeExpansionProjection[pathKey].Keys
            // before we replaced it with DirectoryAggregation.
            return DirectoryAggregation.Aggregate(
                _nextLayer.List(path),
                _treeExpansionProjectionSorted[pathKey],
                true);
        }

        public IEnumerable<VfsEntry>? List(string path)
        {
            var pathKey = path.TrimStart(new[] { '\\', '/' }).ToLowerInvariant();
            if (_treeExpansionProjectionSorted.ContainsKey(pathKey))
            {
                return ListAggregated(pathKey, path);
            }
            else
            {
                // Otherwise this layer isn't responsible for this path at all, delegate
                // entirely to the next layer down.
                return _nextLayer?.List(path);
            }
        }

        public VfsEntryExistence Exists(string path)
        {
            var pathKey = path.TrimStart(new[] { '\\', '/' }).ToLowerInvariant();

            if (_treeProjection.ContainsKey(pathKey))
            {
                return VfsEntryExistence.DirectoryExists;
            }
            else if (_fileProjection.ContainsKey(pathKey))
            {
                return VfsEntryExistence.FileExists;
            }
            else if (_nextLayer != null)
            {
                return _nextLayer.Exists(path);
            }
            else
            {
                return VfsEntryExistence.DoesNotExist;
            }
        }

        private async Task EnsurePackExists(string packHash)
        {
            var packPath = Path.Combine(_cachePath, "packs", packHash);
            if (File.Exists(packPath))
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(packPath)!);

            var pack = _packs[packHash];

            using (var client = new HttpClient())
            {
#if ENABLE_TRACE_LOGS
                _logger.LogTrace($"{pack.RemotePath}/{pack.Hash}: Fetching pack on demand... ({pack.CompressedSize / 1024 / 1024}MB to download, {pack.Size / 1024 / 1024}MB when extracted)");
#endif
                using (var stream = await client.GetStreamAsync($"http://cdn.unrealengine.com/dependencies/{pack.RemotePath}/{pack.Hash}").ConfigureAwait(false))
                {
                    using (var decompressedStream = new GZipStream(stream, CompressionMode.Decompress, true))
                    {
                        using (var writer = new FileStream(packPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            await decompressedStream.CopyToAsync(writer).ConfigureAwait(false);
                        }
                    }
                }
#if ENABLE_TRACE_LOGS
                _logger.LogTrace($"{pack.RemotePath}/{pack.Hash}: Successfully fetched pack.");
#endif
            }
        }

        public VfsEntry? GetInfo(string path)
        {
            var pathKey = path.TrimStart(new[] { '\\', '/' }).ToLowerInvariant();
            if (!_files.ContainsKey(pathKey))
            {
                var nextLayerDirectory = _nextLayer?.GetInfo(path);
                if (nextLayerDirectory == null)
                {
                    // For directories that only exist from the Git dependencies layer,
                    // we need to generate a projection entry for them.
                    if (_treeProjection.ContainsKey(pathKey))
                    {
                        return _treeProjection[pathKey];
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return nextLayerDirectory;
                }
            }

            var blob = _blobs[_files[pathKey].Hash!];
            return new VfsEntry
            {
                Name = Path.GetFileName(path),
                CreationTime = _nextLayer.Created,
                LastWriteTime = _nextLayer.Created,
                LastAccessTime = _nextLayer.Created,
                ChangeTime = _nextLayer.Created,
                Attributes = FileAttributes.Archive,
                Size = blob.Size,
            };
        }

        public IVfsFileHandle<IVfsFile>? OpenFile(
            string path,
            FileMode fileMode,
            FileAccess fileAccess,
            FileShare fileShare,
            ref VfsEntry? metadata)
        {
            bool acquiredGlobalSemaphore = false;
            if (Environment.GetEnvironmentVariable("UEFS_GIT_TEST_LOCK_MODE") == "global-semaphore")
            {
                _globalSemaphore.Wait();
                acquiredGlobalSemaphore = true;
            }

            try
            {
                if (!fileMode.IsReadOnlyAccess(fileAccess))
                {
                    // We don't support read-write.
                    throw new NotSupportedException("GitDependenciesProjectionLayer OpenFile !IsReadOnlyAccess");
                }

                var pathKey = path.TrimStart(new[] { '\\', '/' }).ToLowerInvariant();
                if (!_files.ContainsKey(pathKey))
                {
                    return _nextLayer?.OpenFile(path, fileMode, fileAccess, fileShare, ref metadata);
                }

                var blob = _blobs[_files[pathKey].Hash!];
                var packHash = blob.PackHash;
                using (KeyedSemaphores.KeyedSemaphore.Lock($"git-deps-pack-download:{packHash?.ToLowerInvariant()}"))
                {
                    // Run on another thread since Dokan doesn't support awaiting.
                    var backgroundTask = Task.Run(async () => await EnsurePackExists(packHash!).ConfigureAwait(false));
                    // @todo: Is this safe?
                    backgroundTask.Wait();
                }

                metadata = new VfsEntry
                {
                    Name = Path.GetFileName(path),
                    CreationTime = _nextLayer.Created,
                    LastWriteTime = _nextLayer.Created,
                    LastAccessTime = _nextLayer.Created,
                    ChangeTime = _nextLayer.Created,
                    Attributes = FileAttributes.Archive,
                    Size = blob.Size,
                };
                return _localIoVfsFileFactory.CreateOffsetVfsFileHandle(
                    Path.Combine(_cachePath, "packs", packHash!),
                    blob.PackOffset,
                    blob.Size);
            }
            finally
            {
                if (acquiredGlobalSemaphore)
                {
                    _globalSemaphore.Release();
                }
            }
        }

        public bool CreateDirectory(string path)
        {
            throw new NotSupportedException("GitDependenciesProjectionLayer CreateDirectory");
        }

        public bool MoveFile(string oldPath, string newPath, bool replace)
        {
            throw new NotSupportedException("GitDependenciesProjectionLayer MoveFile");
        }

        public bool DeleteFile(string path)
        {
            throw new NotSupportedException("GitDependenciesProjectionLayer DeleteFile");
        }

        public bool DeleteDirectory(string path)
        {
            throw new NotSupportedException("GitDependenciesProjectionLayer DeleteDirectory");
        }

        public bool SetBasicInfo(string path, uint? attributes, DateTimeOffset? creationTime, DateTimeOffset? lastAccessTime, DateTimeOffset? lastWriteTime, DateTimeOffset? changeTime)
        {
            throw new NotSupportedException("GitDependenciesProjectionLayer SetBasicInfo");
        }
    }
}
