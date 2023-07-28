namespace Redpoint.OpenGE.Executor.CompilerDb
{
    using Microsoft.Extensions.Logging;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Tenray.ZoneTree;
    using Tenray.ZoneTree.Core;
    using Tenray.ZoneTree.Options;
    using Tenray.ZoneTree.Serializers;

    internal class InMemoryCompilerDb : ICompilerDb
    {
        private readonly ILogger<InMemoryCompilerDb> _logger;
        private readonly ConcurrentDictionary<string, CompilerDbEntry> _fileCache;
        private readonly SemaphoreSlim _fileCacheLock;
        private readonly ConcurrentDictionary<string, bool> _fileExistenceCache;
        private readonly IZoneTree<string, DiskRecord> _diskCache;

        private record class DiskRecord
        {
            public required DateTimeOffset LastModified;
            public required string[] ImmediateDependsOn;
        }

        private class DiskRecordSerializer : ISerializer<DiskRecord>
        {
            public DiskRecord Deserialize(byte[] bytes)
            {
                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var utcTicks = reader.ReadInt64();
                        var depCount = reader.ReadInt32();
                        var deps = new string[depCount];
                        for (var i = 0; i < depCount; i++)
                        {
                            deps[i] = reader.ReadString();
                        }
                        return new DiskRecord
                        {
                            LastModified = new DateTimeOffset(utcTicks, TimeSpan.Zero),
                            ImmediateDependsOn = deps,
                        };
                    }
                }
                throw new NotImplementedException();
            }

            public byte[] Serialize(in DiskRecord entry)
            {
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream))
                    {
                        writer.Write(entry.LastModified.UtcTicks);
                        writer.Write(entry.ImmediateDependsOn.Length);
                        for (var i = 0; i < entry.ImmediateDependsOn.Length; i++)
                        {
                            writer.Write(entry.ImmediateDependsOn[i]);
                        }
                    }
                    return stream.ToArray();
                }
            }
        }

        public InMemoryCompilerDb(
            ILogger<InMemoryCompilerDb> logger)
        {
            _logger = logger;
            _fileCache = new ConcurrentDictionary<string, CompilerDbEntry>(OperatingSystem.IsWindows() ? StringComparer.InvariantCultureIgnoreCase : StringComparer.InvariantCulture);
            _fileCacheLock = new SemaphoreSlim(1);
            _fileExistenceCache = new ConcurrentDictionary<string, bool>();

            _diskCache = new ZoneTreeFactory<string, DiskRecord>()
                .SetDataDirectory(@"C:\ProgramData\UET\CompilerDb")
                .SetKeySerializer(new Utf8StringSerializer())
                .SetValueSerializer(new DiskRecordSerializer())
                .ConfigureWriteAheadLogOptions(configure =>
                {
                    // @todo: This can be faster if we have a clear exit callback.
                    configure.WriteAheadLogMode = WriteAheadLogMode.Sync;
                })
                .OpenOrCreate();
        }

        private string NormalizePath(string path)
        {
            return path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);
        }

        private async Task<IEnumerable<string>> ProcessFileAsync(
            string filePath,
            IReadOnlyList<DirectoryInfo> includeDirectories,
            IReadOnlyList<DirectoryInfo> systemIncludeDirectories,
            IReadOnlyDictionary<string, string> globalDefinitions,
            HashSet<string> scanCache,
            CancellationToken cancellationToken,
            bool provideFullDependencyTree)
        {
            CompilerDbEntry entry;
            do
            {
                bool isProcessing;
                await _fileCacheLock.WaitAsync(cancellationToken);
                try
                {
                    var path = NormalizePath(filePath);
                    if (scanCache.Contains(path))
                    {
                        // Don't re-process or wait on the same file in the same
                        // compilation unit, since this can lead to circular dependencies.
                        return new string[0];
                    }
                    scanCache.Add(path);
                    if (_fileCache.TryGetValue(path, out entry!))
                    {
                        isProcessing = false;
                    }
                    else if (_diskCache.TryGet(path, out var diskRecord))
                    {
                        entry = new CompilerDbEntry
                        {
                            Path = path,
                            ImmediateDependsOn = diskRecord.ImmediateDependsOn,
                        };
                        entry.Ready.Open();
                        _fileCache.TryAdd(path, entry);
                        isProcessing = false;
                    }
                    else
                    {
                        entry = new CompilerDbEntry
                        {
                            Path = filePath,
                        };
                        _fileCache.TryAdd(path, entry);
                        isProcessing = true;
                    }
                }
                finally
                {
                    _fileCacheLock.Release();
                }

                if (isProcessing)
                {
                    try
                    {
                        entry.ImmediateDependsOn =
                            await ProcessUniqueFileAsync(
                                entry,
                                includeDirectories,
                                systemIncludeDirectories,
                                globalDefinitions,
                                scanCache,
                                cancellationToken);
                        _diskCache.Upsert(
                            entry.Path,
                            new DiskRecord
                            {
                                LastModified = File.GetLastWriteTimeUtc(entry.Path),
                                ImmediateDependsOn = entry.ImmediateDependsOn,
                            });
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        // We didn't finish properly.
                        _fileCache.Remove(filePath, out _);
                        entry.WasCancelled = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to process file: {ex}");
                        entry.WasCancelled = true;
                    }
                    finally
                    {
                        entry.Ready.Open();
                    }
                }
                else
                {
                    await entry.Ready.WaitAsync(cancellationToken);
                    if (entry.WasCancelled)
                    {
                        // The original task was cancelled, try again.
                        cancellationToken.ThrowIfCancellationRequested();
                        continue;
                    }
                    break;
                }
            }
            while (true);

            if (!provideFullDependencyTree)
            {
                return entry.ImmediateDependsOn;
            }

            var dependencies = new HashSet<string>();
            void ExpandDependenciesAsync(string current)
            {
                // Console.WriteLine($"Expanding {current}");
                CompilerDbEntry target;
                if (_fileCache.ContainsKey(current))
                {
                    target = _fileCache[current];
                }
                else if (_diskCache.TryGet(current, out var diskRecord))
                {
                    target = new CompilerDbEntry
                    {
                        Path = current,
                        ImmediateDependsOn = diskRecord.ImmediateDependsOn,
                    };
                    target.Ready.Open();
                    _fileCache.TryAdd(current, target);
                }
                else
                {
                    throw new NotSupportedException("Expected to have target file in memory or disk cache!");
                }
                // Console.WriteLine($"Has {target.ImmediateDependsOn.Length} deps for {current}");
                if (!dependencies.Contains(current))
                {
                    // Console.WriteLine($"Expanding processing {current}");
                    dependencies.Add(current);
                    foreach (var dep in target.ImmediateDependsOn)
                    {
                        ExpandDependenciesAsync(dep);
                    }
                }
            }
            ExpandDependenciesAsync(filePath);
            return dependencies;
        }

        private async Task<string[]> ProcessUniqueFileAsync(
            CompilerDbEntry entry,
            IReadOnlyList<DirectoryInfo> includeDirectories,
            IReadOnlyList<DirectoryInfo> systemIncludeDirectories,
            IReadOnlyDictionary<string, string> globalDefinitions,
            HashSet<string> scanCache,
            CancellationToken cancellationToken)
        {
            var st = Stopwatch.StartNew();
            var lines = await File.ReadAllLinesAsync(entry.Path);
            var includes = new List<string>();
            var systemIncludes = new List<string>();
            foreach (var line in lines)
            {
                var l = line.TrimStart();
                if (l.StartsWith("#include"))
                {
                    var c = l.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (c.Length >= 2)
                    {
                        var include = c[1];
                        if (include[0] == '"')
                        {
                            includes.Add(include.Trim('"'));
                        }
                        else if (include[0] == '<')
                        {
                            systemIncludes.Add(include.TrimStart('<').TrimEnd('>'));
                        }
                        else if (include.StartsWith("COMPILED_PLATFORM_HEADER("))
                        {
                            if (!globalDefinitions.ContainsKey("UBT_COMPILED_PLATFORM"))
                            {
                                Console.WriteLine($"{globalDefinitions.Count} definitions");
                                foreach (var kv in globalDefinitions)
                                {
                                    Console.WriteLine($"{kv.Key}={kv.Value}");
                                }
                            }

                            var platformName = globalDefinitions.ContainsKey("OVERRIDE_PLATFORM_HEADER_NAME")
                                ? globalDefinitions["OVERRIDE_PLATFORM_HEADER_NAME"]
                                : globalDefinitions["UBT_COMPILED_PLATFORM"];
                            var platformHeader = include.Substring("COMPILED_PLATFORM_HEADER(".Length).TrimEnd(')');
                            includes.Add($"{platformName}/{platformName}{platformHeader}");
                        }
                        else
                        {
                            _logger.LogWarning($"Unknown #include line: {line}");
                        }
                    }
                }
            }
            var immediatelyDependsOn = new ConcurrentBag<string>();
            async Task ProcessIncludeSet(List<string> includes, IReadOnlyList<DirectoryInfo> includeDirectories)
            {
                await Parallel.ForEachAsync(
                    includes,
                    new ParallelOptions
                    {
                        CancellationToken = cancellationToken,
                        MaxDegreeOfParallelism = 32,
                    },
                    async (include, ct) =>
                    {
                        if (Path.IsPathRooted(include))
                        {
                            if (_fileExistenceCache.GetOrAdd(include, File.Exists))
                            {
                                var fi = NormalizePath(include);
                                immediatelyDependsOn.Add(fi);
                                await ProcessFileAsync(
                                    fi,
                                    includeDirectories,
                                    systemIncludeDirectories,
                                    globalDefinitions,
                                    scanCache,
                                    cancellationToken,
                                    provideFullDependencyTree: false);
                            }
                        }
                        else
                        {
                            foreach (var dir in includeDirectories)
                            {
                                var fullInclude = NormalizePath(Path.Combine(dir.FullName, include));
                                if (_fileExistenceCache.GetOrAdd(fullInclude, File.Exists))
                                {
                                    immediatelyDependsOn.Add(fullInclude);
                                    await ProcessFileAsync(
                                        fullInclude,
                                        includeDirectories,
                                        systemIncludeDirectories,
                                        globalDefinitions,
                                        scanCache,
                                        cancellationToken,
                                        provideFullDependencyTree: false);
                                    // Stop after the first match for an include.
                                    return;
                                }
                            }
                        }
                    });
            }
            await Task.WhenAll(
                ProcessIncludeSet(includes, includeDirectories),
                ProcessIncludeSet(systemIncludes, systemIncludeDirectories));
            return immediatelyDependsOn.ToArray();
        }

        public async Task<IEnumerable<string>> ProcessRootFileAsync(
            string filePath,
            IReadOnlyList<DirectoryInfo> includeDirectories,
            IReadOnlyList<DirectoryInfo> systemIncludeDirectories,
            IReadOnlyDictionary<string, string> globalDefinitions,
            CancellationToken cancellationToken)
        {
            return await ProcessFileAsync(
                filePath,
                includeDirectories,
                systemIncludeDirectories,
                globalDefinitions,
                new HashSet<string>(),
                cancellationToken,
                provideFullDependencyTree: true);
        }
    }
}
