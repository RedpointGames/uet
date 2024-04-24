namespace Redpoint.Uefs.Daemon.Service
{
    using Grpc.Core;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Redpoint.Concurrency;
    using Redpoint.GrpcPipes;
    using Redpoint.Uefs.Daemon.Abstractions;
    using Redpoint.Uefs.Daemon.Database;
    using Redpoint.Uefs.Daemon.PackageStorage;
    using Redpoint.Uefs.Daemon.Service.Mounting;
    using Redpoint.Uefs.Daemon.State;
    using Redpoint.Uefs.Daemon.Transactional.Abstractions;
    using Redpoint.Uefs.Package;
    using Redpoint.Uefs.Protocol;
    using System.Text.Json;

    internal sealed class UefsDaemon : IUefsDaemon, IAsyncDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<UefsDaemon> _logger;
        private readonly IMounter<MountPackageFileRequest> _packageFileMounter;
        private readonly IMounter<MountGitCommitRequest> _gitCommitMounter;
        private readonly IGrpcPipeFactory _grpcPipeFactory;
        private readonly IPackageStorage _packageStorage;
        private readonly IPackageMounterFactory[] _mounterFactories;
        private readonly string _rootPath;
        private readonly Dictionary<string, CurrentUefsMount> _currentMounts;
        private readonly Dictionary<string, DockerVolume> _dockerVolumes;
        private readonly ITransactionalDatabase _transactionalDatabase;
        private readonly DaemonDatabase _database;
        private readonly Concurrency.Semaphore _databaseSaveSemaphore = new Concurrency.Semaphore(1);
        private IGrpcPipeServer<UefsGrpcService>? _grpcService;

        public UefsDaemon(
            IServiceProvider serviceProvider,
            ILogger<UefsDaemon> logger,
            IPackageStorageFactory packageStorageFactory,
            IMounter<MountPackageFileRequest> packageFileMounter,
            IMounter<MountGitCommitRequest> gitCommitMounter,
            ITransactionalDatabaseFactory transactionalDatabaseFactory,
            IGrpcPipeFactory grpcPipeFactory,
            string rootPath)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _packageFileMounter = packageFileMounter;
            _gitCommitMounter = gitCommitMounter;
            _grpcPipeFactory = grpcPipeFactory;
            _packageStorage = packageStorageFactory.CreatePackageStorage(rootPath);
            _mounterFactories = serviceProvider.GetServices<IPackageMounterFactory>().ToArray();
            _rootPath = rootPath;
            _currentMounts = new Dictionary<string, CurrentUefsMount>();
            _dockerVolumes = new Dictionary<string, DockerVolume>();
            _transactionalDatabase = transactionalDatabaseFactory.CreateTransactionalDatabase();

            var databasePath = Path.Combine(
                _rootPath,
                "db.json");
            if (File.Exists(databasePath))
            {
                _database = JsonSerializer.Deserialize(
                    File.ReadAllText(databasePath)!,
                    DaemonDatabaseJsonSerializerContext.WithStringEnums.DaemonDatabase)!;
            }
            else
            {
                _database = new DaemonDatabase();
            }
        }

        public Dictionary<string, DockerVolume> DockerVolumes => _dockerVolumes;
        public IReadOnlyDictionary<string, CurrentUefsMount> CurrentMounts => _currentMounts;
        public IPackageStorage PackageStorage => _packageStorage;
        public string StoragePath => _rootPath;
        public ITransactionalDatabase TransactionalDatabase => _transactionalDatabase;

        public async Task StartAsync()
        {
            var mustSaveDatabase = false;

            // Delete any temporary write layers that are present on the system.
            if (OperatingSystem.IsWindows())
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    var tempPath = Path.Combine(drive.Name, "TEMP", "uefs-write-layers");
                    if (Directory.Exists(tempPath))
                    {
                        foreach (var directory in Directory.EnumerateDirectories(tempPath))
                        {
                            var deletableFiles = new[]
                            {
                                "writelayer.vhd",
                                "writelayer-none.vhd",
                                "writelayer-ro.vhd",
                                "writelayer-discard.vhd"
                            };
                            foreach (var file in deletableFiles.Select(x => Path.Combine(directory, x)))
                            {
                                if (File.Exists(file))
                                {
                                    _logger.LogInformation($"Attempting to delete temporary write layer from previous run: {file}");
                                    try
                                    {
                                        File.Delete(file);
                                    }
                                    catch (IOException)
                                    {
                                        _logger.LogWarning($"Attempting to delete temporary write layer from previous run: {file}");
                                    }
                                }
                            }
                            if (Directory.GetFileSystemEntries(directory).Length == 0)
                            {
                                _logger.LogInformation($"Attempting to delete temporary directory from previous run: {directory}");
                                Directory.Delete(directory);
                            }
                        }
                    }
                }
            }

            // Restore mounts that are currently present on the system.
            var mountIdCache = _database.MountIdCache;
            _database.MountIdCache = new Dictionary<string, string>(); // Replace it; we'll restore all the entries we actually have.
            var persistentMounts = _database.GetPersistentMounts();
            foreach (var mounterFactory in _mounterFactories)
            {
                await using (mounterFactory.CreatePackageMounter().AsAsyncDisposable(out var mounter).ConfigureAwait(false))
                {
                    foreach (var mount in await mounter.ImportExistingMountsAtStartupAsync().ConfigureAwait(false))
                    {
                        var existingId = mountIdCache.FirstOrDefault(x => PathUtils.IsPathEqual(x.Key, mount.mountPath)).Value;
                        var newId = existingId ?? Guid.NewGuid().ToString();

                        _logger.LogInformation($"Importing existing mount at {mount.mountPath} as ID {newId} on startup...");

                        var persistenceInfo = persistentMounts.ContainsKey(mount.mountPath) ? persistentMounts[mount.mountPath] : null;

                        _currentMounts.Add(
                            newId,
                            new CurrentPackageUefsMount(
                                mount.packagePath,
                                mount.mountPath,
                                persistenceInfo?.TagHint,
                                mount.mounter)
                            {
                                WriteScratchPersistence = persistenceInfo?.PersistenceMode ?? WriteScratchPersistence.DiscardOnUnmount,
                                StartupBehaviour = StartupBehaviour.MountOnStartup,
                            });
                        _database.MountIdCache[mount.mountPath] = newId;
                        mustSaveDatabase = true;
                    }
                }
            }
            if (mountIdCache.Count > 0)
            {
                mustSaveDatabase = true;
            }

            string AllocateMountId()
            {
                var id = Guid.NewGuid().ToString();
                while (_currentMounts.ContainsKey(id))
                {
                    id = Guid.NewGuid().ToString();
                }
                return id;
            }

            // Restore persistent mounts.
            var persistentMountsToRemove = new List<string>();
            foreach (var persistentMount in _database.GetPersistentMounts())
            {
                if (_currentMounts.Any(x => x.Value.MountPath == persistentMount.Key))
                {
                    // This is already mounted on the system. No need to mount it again.
                    continue;
                }

                try
                {
                    if (!string.IsNullOrWhiteSpace(persistentMount.Value.PackagePath))
                    {
                        await _packageFileMounter.MountAsync(
                            this,
                            new MountContext
                            {
                                MountId = AllocateMountId(),
                                TrackedPid = null,
                                IsBeingMountedOnStartup = true,
                            },
                            new MountPackageFileRequest
                            {
                                MountRequest = new MountRequest
                                {
                                    MountPath = persistentMount.Key,
                                    StartupBehaviour = StartupBehaviour.MountOnStartup,
                                    WriteScratchPersistence = persistentMount.Value.PersistenceMode,
                                    WriteScratchPath = persistentMount.Value.WriteStoragePath,
                                },
                                Path = persistentMount.Value.PackagePath,
                            },
                            _ => Task.CompletedTask,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    else if (!string.IsNullOrWhiteSpace(persistentMount.Value.GitUrl) &&
                        !string.IsNullOrWhiteSpace(persistentMount.Value.GitCommit))
                    {
                        await _gitCommitMounter.MountAsync(
                            this,
                            new MountContext
                            {
                                MountId = AllocateMountId(),
                                TrackedPid = null,
                                IsBeingMountedOnStartup = true,
                            },
                            new MountGitCommitRequest
                            {
                                MountRequest = new MountRequest
                                {
                                    MountPath = persistentMount.Key,
                                    StartupBehaviour = StartupBehaviour.MountOnStartup,
                                    WriteScratchPersistence = persistentMount.Value.PersistenceMode,
                                    WriteScratchPath = persistentMount.Value.WriteStoragePath,
                                },
                                Url = persistentMount.Value.GitUrl,
                                Commit = persistentMount.Value.GitCommit,
                            },
                            _ => Task.CompletedTask,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogError($"Unknown persistent mount specification: {JsonSerializer.Serialize(persistentMount.Value, DaemonDatabaseJsonSerializerContext.WithStringEnums.DaemonDatabasePersistentMount)}");
                        persistentMountsToRemove.Add(persistentMount.Key);
                    }
                }
                catch (RpcException ex)
                {
                    _logger.LogError(ex, $"Failed to restore persistent mount: {ex}");
                    persistentMountsToRemove.Add(persistentMount.Key);
                }
            }
            if (persistentMountsToRemove.Count > 0)
            {
                foreach (var toRemove in persistentMountsToRemove)
                {
                    _database.RemovePersistentMount(toRemove);
                }
                mustSaveDatabase = true;
            }

            if (mustSaveDatabase)
            {
                await SavePersistentDatabaseAsync().ConfigureAwait(false);
            }

            _grpcService = _grpcPipeFactory.CreateServer(
                "UEFS",
                GrpcPipeNamespace.Computer,
                _serviceProvider.GetRequiredService<UefsGrpcService>());
            await _grpcService.StartAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (_grpcService != null)
            {
                await _grpcService.StopAsync().ConfigureAwait(false);
                await _grpcService.DisposeAsync().ConfigureAwait(false);
                _grpcService = null;
            }

            // Cleanly shut down any package mounts and volumes.
            foreach (var volume in DockerVolumes)
            {
                _logger.LogInformation($"Cleaning up Docker volume located at: {volume.Value?.Mountpoint}");
                if (volume.Value?.PackageMounter is IPackageMounter mounter && mounter != null)
                {
                    await mounter.DisposeAsync().ConfigureAwait(false);
                }
            }
            foreach (var mount in CurrentMounts)
            {
                _logger.LogInformation($"Cleaning up mount located at: {mount.Value?.MountPath}");
                if (mount.Value is CurrentUefsMount mountValue && mountValue != null)
                {
                    await mountValue.DisposeUnderlyingAsync().ConfigureAwait(false);
                }
            }

            // Stop any running processes.
            _logger.LogInformation($"Stopping running processes...");
            PackageStorage.StopProcesses();

            _logger.LogInformation($"Daemon shutdown complete.");
        }

        public bool IsPathMountPath(string path)
        {
            return _database.MountIdCache.ContainsKey(path);
        }

        public async Task AddCurrentMountAsync(string id, CurrentUefsMount mount)
        {
            _currentMounts.Add(
                id,
                mount);
            _database.MountIdCache[mount.MountPath!] = id;
            await SavePersistentDatabaseAsync().ConfigureAwait(false);
        }

        public async Task RemoveCurrentMountAsync(string id)
        {
            _currentMounts.Remove(id);
            var impactedKeys = _database.MountIdCache.Where(x => x.Value == id).Select(x => x.Key).ToList();
            foreach (var key in impactedKeys)
            {
                _database.MountIdCache.Remove(key);
            }
            if (impactedKeys.Count > 0)
            {
                await SavePersistentDatabaseAsync().ConfigureAwait(false);
            }
        }

        public async Task AddPersistentMountAsync(string mountPath, DaemonDatabasePersistentMount persistentMount)
        {
            _database.AddPersistentMount(mountPath, persistentMount);
            await SavePersistentDatabaseAsync().ConfigureAwait(false);
        }

        public async Task RemovePersistentMountAsync(string mountPath)
        {
            _database.RemovePersistentMount(mountPath);
            await SavePersistentDatabaseAsync().ConfigureAwait(false);
        }

        private async Task SavePersistentDatabaseAsync()
        {
            await _databaseSaveSemaphore.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                var databasePath = Path.Combine(
                    _rootPath,
                    "db.json");
                await File.WriteAllTextAsync(databasePath, JsonSerializer.Serialize(_database, DaemonDatabaseJsonSerializerContext.WithStringEnums.DaemonDatabase)).ConfigureAwait(false);
            }
            finally
            {
                _databaseSaveSemaphore.Release();
            }
        }
    }
}
