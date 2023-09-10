namespace Redpoint.Git.Managed
{
    using Microsoft.Extensions.Logging;
    using System.Collections.Concurrent;
    using Redpoint.Git.Managed.Operation;
    using Redpoint.Concurrency;
    using Redpoint.Git.Managed.Packfile;
    using BitFaster.Caching.Lru;
    using BitFaster.Caching;
    using System.Text;
    using System.IO.Compression;
    using Redpoint.Tasks;

    internal class GitExecutionEngine : IDisposable
    {
        private readonly AwaitableConcurrentQueue<GitOperation> _pendingOperations;
        private readonly ILogger<GitExecutionEngine> _logger;
        // @bug: I'm pretty sure this can dispose the packfiles while we still
        // have streams open. Packfile needs to be updated to defer it's actual
        // Dispose operations until the returned streams are also disposed.
        private readonly ICache<string, Packfile.Packfile> _packfileCache;
        private readonly ICache<string, PackfileIndex> _packfileIndexCache;
        private readonly ICache<string, bool> _looseObjectExistenceCache;
        private readonly ICache<string, FileInfo[]> _packfileListingCache;
        private readonly ITaskSchedulerScope _operationTasksScope;
        private readonly Task[] _operationTasks;

        public GitExecutionEngine(
            ILogger<GitExecutionEngine> logger,
            ITaskScheduler taskScheduler)
        {
            _pendingOperations = new AwaitableConcurrentQueue<GitOperation>();
            _logger = logger;
            _packfileCache = new ConcurrentLruBuilder<string, Packfile.Packfile>()
                .WithExpireAfterWrite(TimeSpan.FromMinutes(1))
                .WithCapacity(20)
                .WithAtomicGetOrAdd()
                .Build();
            _packfileIndexCache = new ConcurrentLruBuilder<string, PackfileIndex>()
                .WithExpireAfterWrite(TimeSpan.FromMinutes(1))
                .WithCapacity(20)
                .WithAtomicGetOrAdd()
                .Build();
            _looseObjectExistenceCache = new ConcurrentLruBuilder<string, bool>()
                .WithExpireAfterWrite(TimeSpan.FromMinutes(1))
                .WithCapacity(1024 * 16)
                .WithAtomicGetOrAdd()
                .Build();
            _packfileListingCache = new ConcurrentLruBuilder<string, FileInfo[]>()
                .WithExpireAfterWrite(TimeSpan.FromMinutes(1))
                .WithCapacity(20)
                .WithAtomicGetOrAdd()
                .Build();
            _operationTasksScope = taskScheduler.CreateSchedulerScope("GitExecutionEngine", CancellationToken.None);
            _operationTasks = Enumerable.Range(0, Math.Max(4, Environment.ProcessorCount))
                .Select(x => _operationTasksScope.RunAsync($"Core{x}", CancellationToken.None, RunAsync))
                .ToArray();
        }

        public Action<Exception>? OnInternalException { get; set; }

        private async Task RunOperationAsync(
            GitOperation operation,
            CancellationToken cancellationToken)
        {
            switch (operation)
            {
                case CheckoutCommitGitOperation checkout:

                    break;
                case GetObjectGitOperation getObject:
                    {
                        var packfiles = _packfileListingCache.GetOrAdd(
                            getObject.GitDirectory.FullName,
                            path =>
                            {
                                var packPath = new DirectoryInfo(Path.Combine(
                                    path,
                                    "objects",
                                    "pack"));
                                FileInfo[] packfiles = Array.Empty<FileInfo>();
                                if (packPath.Exists)
                                {
                                    packfiles = packPath.GetFiles("*.pack");
                                }
                                return packfiles;
                            });
                        var cts = new CancellationTokenSource();
                        var fptp = new FirstPastThePost<GitObjectInfo>(
                            cts,
                            packfiles.Length + 1,
                            async result =>
                            {
                                await getObject.OnResultAsync(result);
                            });
                        foreach (var packfile in packfiles)
                        {
                            EnqueueOperation(new GetObjectFromPackfileGitOperation
                            {
                                Packfile = packfile,
                                Sha = getObject.Sha,
                                Result = fptp,
                            });
                        }
                        EnqueueOperation(new GetLooseObjectGitOperation
                        {
                            GitDirectory = getObject.GitDirectory,
                            Sha = getObject.Sha,
                            Result = fptp,
                        });
                    }
                    break;
                case GetObjectFromPackfileGitOperation getObjectFromPackfile:
                    {
                        var packfile = _packfileCache.GetOrAdd(
                            getObjectFromPackfile.Packfile.FullName,
                            path => new Packfile.Packfile(path));
                        var packfileIndex = _packfileIndexCache.GetOrAdd(
                            getObjectFromPackfile.Packfile.FullName.Substring(
                                0,
                                getObjectFromPackfile.Packfile.FullName.Length - 5) + ".idx",
                            path => new PackfileIndex(path));
                        if (packfile.GetRawPackfileEntry(
                            packfileIndex,
                            getObjectFromPackfile.Sha,
                            out var type,
                            out var size,
                            out var data))
                        {
                            await getObjectFromPackfile.Result.ReceiveResultAsync(
                                new GitObjectInfo
                                {
                                    Type = type,
                                    Size = size,
                                    Data = data,
                                });
                        }
                        else
                        {
                            await getObjectFromPackfile.Result.ReceiveNoResultAsync();
                        }
                    }
                    break;
                case GetLooseObjectGitOperation getLooseObject:
                    {
                        var stringSha = getLooseObject.Sha.ToString();
                        var loosePath = Path.Combine(
                            getLooseObject.GitDirectory.FullName,
                            "objects",
                            stringSha.Insert(2, Path.DirectorySeparatorChar.ToString()));
                        if (_looseObjectExistenceCache.GetOrAdd(loosePath, File.Exists))
                        {
                            var fileStream = new FileStream(
                                loosePath,
                                FileMode.Open,
                                FileAccess.Read,
                                FileShare.Read);
                            var stream = new ZLibStream(
                                fileStream,
                                CompressionMode.Decompress,
                                leaveOpen: false);
                            var buffer = new byte[128];
                            int spaceIndex = 0;
                            int nullIndex = 0;
                            for (int i = 0; i < 128; i++)
                            {
                                // @note: We must read one byte at a time because the zlib stream can't be seeked on (so if we over-read, the consumer of the stream has no way to rewind to see the start of the file).
                                var b = stream.ReadByte();
                                if (b == -1)
                                {
                                    throw new InvalidOperationException("Loose object file was too small for header!");
                                }
                                buffer[i] = (byte)b;
                                if (buffer[i] == ' ')
                                {
                                    spaceIndex = i;
                                }
                                if (buffer[i] == '\0')
                                {
                                    nullIndex = i;
                                    break;
                                }
                            }
                            if (nullIndex == 0)
                            {
                                throw new InvalidOperationException("Unable to find end of Git loose object header!");
                            }
                            var type = Encoding.ASCII.GetString(buffer, 0, spaceIndex);
                            var size = ulong.Parse(Encoding.ASCII.GetString(
                                buffer,
                                spaceIndex + 1,
                                nullIndex - (spaceIndex + 1)));
                            await getLooseObject.Result.ReceiveResultAsync(
                                new GitObjectInfo
                                {
                                    Type = type switch
                                    {
                                        "commit" => GitObjectType.Commit,
                                        "tree" => GitObjectType.Tree,
                                        "blob" => GitObjectType.Blob,
                                        "tag" => GitObjectType.Tag,
                                        _ => throw new NotSupportedException($"Unsupported loose object '{type}'"),
                                    },
                                    Size = size,
                                    Data = new OffsetStream(stream, nullIndex + 1),
                                });
                        }
                        else
                        {
                            await getLooseObject.Result.ReceiveNoResultAsync();
                        }
                    }
                    break;
            }
        }

        public void Dispose()
        {
            _operationTasksScope.Dispose();
        }

        internal void EnqueueOperation(GitOperation operation)
        {
            _pendingOperations.Enqueue(operation);
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var nextOperation = await _pendingOperations.DequeueAsync(cancellationToken);

                    try
                    {
                        await RunOperationAsync(nextOperation, cancellationToken);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        OnInternalException?.Invoke(ex);
                        _logger.LogError(ex, $"Failed to run Git operation: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected.
            }
            catch (Exception ex)
            {
                OnInternalException?.Invoke(ex);
                _logger.LogError(ex, $"Critical failure in Git executor loop: {ex.Message}");
            }
        }
    }
}