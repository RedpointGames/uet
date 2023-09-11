using System;

namespace Redpoint.Uefs.Package
{
    using System.Globalization;

    internal sealed class DefaultPackageManifestDataWriter : IPackageManifestDataWriter
    {
        private sealed class WriteMetrics
        {
            public bool writing_directories = false;
            public long written_directories = 0;
            public long total_directories = 0;
            public long written_files = 0;
            public long total_files = 0;
            public long written_bytes = 0;
            public long total_bytes = 0;
            public DateTimeOffset start_time = DateTimeOffset.UtcNow;
            public long last_emitted_console_bytes = 0;
            public long last_emitted_console_files = 0;
            public long last_emitted_console_time = 0;
            public long flush_events = 0;
            public bool done = false;
            public string? last_written_path = null;
        }

        public async Task WriteData(IPackageWriter packageWriter, PackageManifest packageManifest)
        {
            Console.WriteLine($"running all file writes in {(packageWriter.SupportsParallelWrites ? "parallel" : "sequence")}...");

            var metrics = new WriteMetrics();
            metrics.total_directories = packageManifest.Count(x => x.IsDirectory);
            metrics.total_files = packageManifest.FileCount;
            metrics.total_bytes = packageManifest.DataSizeBytes;
            Task progressTask = Task.Run(async () =>
            {
                int count = 0;
                do
                {
                    count++;
                    if (metrics.writing_directories)
                    {
                        long written_directories_loaded = Interlocked.Read(ref metrics.written_directories);
                        double directory_progress = written_directories_loaded / (double)metrics.total_directories * 100.0;

                        if (!Console.IsOutputRedirected)
                        {
                            Console.Write($"\rprogress: {directory_progress.ToString("#####0.00", CultureInfo.InvariantCulture).PadLeft(9)} % ({written_directories_loaded.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)} / {metrics.total_directories.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)}) directories, files will be written after this step finishes, last created: {metrics.last_written_path ?? "(no directory created yet)"}".PadRight(Console.BufferWidth));
                        }
                        else if (count % 50 == 0)
                        {
                            Console.WriteLine($"progress: {directory_progress.ToString("#####0.00", CultureInfo.InvariantCulture).PadLeft(9)} % ({written_directories_loaded.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)} / {metrics.total_directories.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)}) directories, files will be written after this step finishes, last created: {metrics.last_written_path ?? "(no directory created yet)"}");
                        }
                    }
                    else
                    {
                        long written_files_loaded = Interlocked.Read(ref metrics.written_files);
                        long written_bytes_loaded = Interlocked.Read(ref metrics.written_bytes);
                        double file_progress = written_files_loaded / (double)metrics.total_files * 100.0;
                        double bytes_progress = written_bytes_loaded / (double)metrics.total_bytes * 100.0;

                        DateTimeOffset current_time = DateTimeOffset.UtcNow;
                        TimeSpan remaining_time = TimeSpan.FromSeconds(100);
                        if (bytes_progress > 0)
                        {
                            double bytes_ratio = bytes_progress / 100.0;
                            long ms_elapsed = current_time.ToUnixTimeMilliseconds() - metrics.start_time.ToUnixTimeMilliseconds();
                            //        x                1.0
                            //  -------------- = ---------------
                            //    ms_elapsed       bytes_ratio
                            long ms_estimated = (long)(1.0 / bytes_ratio * ms_elapsed);
                            remaining_time = TimeSpan.FromMilliseconds(ms_estimated - ms_elapsed);
                        }

                        long remaining_seconds = (long)remaining_time.TotalSeconds;
                        long remaining_minutes = remaining_seconds / 60;
                        remaining_seconds = remaining_seconds % 60;

                        double bytes_per_second =
            written_bytes_loaded /
            (current_time - metrics.start_time).TotalSeconds;

                        string rate = "b/s";
                        if (bytes_per_second > 1024)
                        {
                            bytes_per_second /= 1024;
                            rate = "KB/s";
                        }
                        if (bytes_per_second > 1024)
                        {
                            bytes_per_second /= 1024;
                            rate = "MB/s";
                        }
                        if (bytes_per_second > 1024)
                        {
                            bytes_per_second /= 1024;
                            rate = "GB/s";
                        }

                        long last_emitted_seconds = metrics.last_emitted_console_time;
                        long flush_events_loaded = metrics.flush_events;

                        if (!Console.IsOutputRedirected)
                        {
                            Console.Write($"\rprogress: {file_progress.ToString("#####0.00", CultureInfo.InvariantCulture).PadLeft(9)} % ({written_files_loaded.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)} / {packageManifest.FileCount.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)}) files, {bytes_progress.ToString("#####0.00", CultureInfo.InvariantCulture).PadLeft(9)} % ({(written_bytes_loaded / 1024 / 1024).ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)} / {(metrics.total_bytes / 1024 / 1024).ToString("######0", CultureInfo.InvariantCulture)}) MB, {bytes_per_second.ToString("#####0.00", CultureInfo.InvariantCulture)} {rate}, {remaining_minutes}:{remaining_seconds.ToString("00", CultureInfo.InvariantCulture)} to go, last wrote: {metrics.last_written_path ?? "(no file finished yet)"}".PadRight(Console.BufferWidth));
                        }
                        else if (count % 50 == 0)
                        {
                            Console.WriteLine($"progress: {file_progress.ToString("#####0.00", CultureInfo.InvariantCulture).PadLeft(9)} % ({written_files_loaded.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)} / {packageManifest.FileCount.ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)}) files, {bytes_progress.ToString("#####0.00", CultureInfo.InvariantCulture).PadLeft(9)} % ({(written_bytes_loaded / 1024 / 1024).ToString("######0", CultureInfo.InvariantCulture).PadLeft(7)} / {(metrics.total_bytes / 1024 / 1024).ToString("######0", CultureInfo.InvariantCulture)}) MB, {bytes_per_second.ToString("#####0.00", CultureInfo.InvariantCulture)} {rate}, {remaining_minutes}:{remaining_seconds.ToString("00", CultureInfo.InvariantCulture)} to go, last wrote: {metrics.last_written_path ?? "(no file finished yet)"}");
                        }

                        metrics.last_emitted_console_time = remaining_seconds;
                        metrics.last_emitted_console_files = written_files_loaded;
                        metrics.last_emitted_console_bytes = written_bytes_loaded;

                    }

                    if (!metrics.done)
                    {
                        await Task.Delay(100).ConfigureAwait(false);
                    }
                }
                while (!metrics.done);
            });

            if (packageWriter.WantsDirectoryWrites)
            {
                metrics.writing_directories = true;

                OnFileWriteComplete onDirectoryWriteComplete = (pathInPackage) =>
                {
                    Interlocked.Add(ref metrics.written_directories, 1);
                    metrics.last_written_path = pathInPackage.Length > 63 ? string.Concat(pathInPackage.AsSpan()[..20], "...", pathInPackage.AsSpan(pathInPackage.Length - 40, 40)) : pathInPackage;
                };

                if (packageWriter.SupportsParallelWrites && packageWriter.SupportsParallelDirectoryWrites)
                {
                    await Parallel.ForEachAsync(
                        packageManifest.Where(x => x.IsDirectory),
                        new ParallelOptions { },
                        async (entry, ct) =>
                        {
                            await packageWriter.WritePackageDirectory(entry, onDirectoryWriteComplete).ConfigureAwait(false);
                        }).ConfigureAwait(false);
                }
                else
                {
                    foreach (var entry in packageManifest.Where(x => x.IsDirectory))
                    {
                        await packageWriter.WritePackageDirectory(entry, onDirectoryWriteComplete).ConfigureAwait(false);
                    }
                }

                metrics.writing_directories = false;
                metrics.last_written_path = null;
            }

            OnFileBytesWritten onFileBytesWritten = (bytes) =>
            {
                Interlocked.Add(ref metrics.written_bytes, bytes);
            };
            OnFileWriteComplete onFileWriteComplete = (pathInPackage) =>
            {
                Interlocked.Add(ref metrics.written_files, 1);
                metrics.last_written_path = pathInPackage.Length > 63 ? string.Concat(pathInPackage.AsSpan()[..20], "...", pathInPackage.AsSpan(pathInPackage.Length - 40, 40)) : pathInPackage;
            };

            if (packageWriter.SupportsParallelWrites)
            {
                await Parallel.ForEachAsync(
                    packageManifest.Where(x => !x.IsDirectory),
                    new ParallelOptions { },
                    async (entry, ct) =>
                    {
                        await packageWriter.WritePackageFile(entry, onFileBytesWritten, onFileWriteComplete).ConfigureAwait(false);
                    }).ConfigureAwait(false);
            }
            else
            {
                foreach (var entry in packageManifest.Where(x => !x.IsDirectory))
                {
                    await packageWriter.WritePackageFile(entry, onFileBytesWritten, onFileWriteComplete).ConfigureAwait(false);
                }
            }

            metrics.done = true;
            await progressTask.ConfigureAwait(false);
        }
    }
}
