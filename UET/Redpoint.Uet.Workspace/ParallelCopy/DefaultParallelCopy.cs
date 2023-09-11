namespace Redpoint.Uet.Workspace.ParallelCopy
{
    using Microsoft.Extensions.Logging;
    using Redpoint.IO;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    internal class DefaultParallelCopy : IParallelCopy
    {
        private readonly ILogger<DefaultParallelCopy>? _logger;

        public DefaultParallelCopy(
            ILogger<DefaultParallelCopy>? logger = null)
        {
            _logger = logger;
        }

        internal record QueuedCopy
        {
            public required FileSystemInfo Source;
            public required string Destination;
        }

        internal record QueuedDelete
        {
            public required string Destination;
        }

        internal record class CopyStats
        {
            public long FilesToCopy;
            public long DirectoriesToCopy;
            public long EntriesToDelete;
            public long BytesToCopy;
        }

        internal async Task RecursiveScanAsync(
            DirectoryInfo sourceDirectoryInfo,
            DirectoryInfo destinationDirectoryInfo,
            ConcurrentQueue<QueuedCopy> itemsToCopy,
            ConcurrentQueue<QueuedDelete> itemsToDelete,
            CopyDescriptor copyDescriptor,
            bool enteredPurgeMode,
            CopyStats copyStats,
            CancellationToken cancellationToken)
        {
            var sourceItems = sourceDirectoryInfo.GetFileSystemInfos().ToDictionary(k => k.Name, v => v, StringComparer.InvariantCultureIgnoreCase);
            var destinationItems = destinationDirectoryInfo.Exists ? destinationDirectoryInfo.GetFileSystemInfos().ToDictionary(k => k.Name, v => v, StringComparer.InvariantCultureIgnoreCase) : new Dictionary<string, FileSystemInfo>();
            foreach (var source in sourceItems)
            {
                var relativePath = Path.GetRelativePath(copyDescriptor.SourcePath, source.Value.FullName).Replace("\\", "/", StringComparison.Ordinal);
                if (copyDescriptor.ExcludePaths.Contains(relativePath, StringComparer.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                var recurseIntoSubdirectory = false;
                if (destinationItems.TryGetValue(source.Key, out var destination))
                {
                    if (source.Value.LastWriteTimeUtc > destination.LastWriteTimeUtc)
                    {
                        // Target is out-of-date.
                        itemsToCopy.Enqueue(new QueuedCopy { Source = source.Value, Destination = Path.Combine(destinationDirectoryInfo.FullName, source.Key) });
                        if (source.Value is FileInfo fi)
                        {
                            Interlocked.Add(ref copyStats.FilesToCopy, 1);
                            Interlocked.Add(ref copyStats.BytesToCopy, fi.Length);
                        }
                        else
                        {
                            Interlocked.Add(ref copyStats.DirectoriesToCopy, 1);
                            recurseIntoSubdirectory = true;
                        }
                    }
                }
                else
                {
                    // Target does not exist.
                    itemsToCopy.Enqueue(new QueuedCopy { Source = source.Value, Destination = Path.Combine(destinationDirectoryInfo.FullName, source.Key) });
                    if (source.Value is FileInfo fi)
                    {
                        Interlocked.Add(ref copyStats.FilesToCopy, 1);
                        Interlocked.Add(ref copyStats.BytesToCopy, fi.Length);
                    }
                    else
                    {
                        Interlocked.Add(ref copyStats.DirectoriesToCopy, 1);
                        recurseIntoSubdirectory = true;
                    }
                }
                if (recurseIntoSubdirectory && source.Value is DirectoryInfo di)
                {
                    await RecursiveScanAsync(
                        di,
                        new DirectoryInfo(Path.Combine(destinationDirectoryInfo.FullName, source.Key)),
                        itemsToCopy,
                        itemsToDelete,
                        copyDescriptor,
                        enteredPurgeMode || copyDescriptor.DirectoriesToRemoveExtraFilesUnder.Contains(di.Name, StringComparer.InvariantCultureIgnoreCase),
                        copyStats,
                        cancellationToken).ConfigureAwait(false);
                }
            }
            if (enteredPurgeMode)
            {
                foreach (var destination in destinationItems)
                {
                    var relativePath = Path.GetRelativePath(copyDescriptor.DestinationPath, destination.Value.FullName).Replace("\\", "/", StringComparison.Ordinal);
                    if (copyDescriptor.ExcludePaths.Contains(relativePath, StringComparer.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (!sourceItems.ContainsKey(destination.Key))
                    {
                        itemsToDelete.Enqueue(new QueuedDelete { Destination = destination.Value.FullName });
                        Interlocked.Add(ref copyStats.EntriesToDelete, 1);
                    }
                }
            }
        }

        public async Task CopyAsync(CopyDescriptor descriptor, CancellationToken cancellationToken)
        {
            var copyItems = new ConcurrentQueue<QueuedCopy>();
            var deleteItems = new ConcurrentQueue<QueuedDelete>();
            var copyStats = new CopyStats();
            await RecursiveScanAsync(
                new DirectoryInfo(descriptor.SourcePath),
                new DirectoryInfo(descriptor.DestinationPath),
                copyItems,
                deleteItems,
                descriptor,
                false,
                copyStats,
                cancellationToken).ConfigureAwait(false);

            var copyTaskCount = Math.Min(Environment.ProcessorCount - 1, copyItems.Count);
            var copyTasks = Enumerable.Range(0, copyTaskCount).Select(x => Task.Run(() =>
            {
                while (copyItems.TryDequeue(out var copyItem))
                {
                    try
                    {
                        if (copyItem.Source is DirectoryInfo di)
                        {
                            if (!Directory.Exists(copyItem.Destination))
                            {
                                Directory.CreateDirectory(copyItem.Destination);
                            }
                            else
                            {
                                Directory.SetLastWriteTimeUtc(copyItem.Destination, copyItem.Source.LastWriteTimeUtc);
                            }
                            Interlocked.Add(ref copyStats.DirectoriesToCopy, -1);
                        }
                        else if (copyItem.Source is FileInfo fi)
                        {
                            // @todo: Use a copier that is compatible with progress monitoring.
                            File.Copy(copyItem.Source.FullName, copyItem.Destination, true);
                            Interlocked.Add(ref copyStats.FilesToCopy, -1);
                            Interlocked.Add(ref copyStats.BytesToCopy, -fi.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Failed to copy '{copyItem.Source.FullName}': {ex.Message}");
                    }
                }
            }));
            await Task.WhenAll(copyTasks).ConfigureAwait(false);

            var deleteTaskCount = Math.Min(Environment.ProcessorCount - 1, deleteItems.Count);
            var deleteTasks = Enumerable.Range(0, deleteTaskCount).Select(x => Task.Run(async () =>
            {
                while (deleteItems.TryDequeue(out var deleteItem))
                {
                    try
                    {
                        if (Directory.Exists(deleteItem.Destination))
                        {
                            await DirectoryAsync.DeleteAsync(deleteItem.Destination, true).ConfigureAwait(false);
                        }
                        else
                        {
                            File.Delete(deleteItem.Destination);
                        }
                        Interlocked.Add(ref copyStats.EntriesToDelete, -1);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning($"Failed to delete '{deleteItem.Destination}': {ex.Message}");
                    }
                }
            }));
        }
    }
}
