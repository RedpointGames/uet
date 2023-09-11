namespace Redpoint.Uefs.Commands.Hash
{
    using System;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using Redpoint.ProgressMonitor;

    internal class DefaultFileHasher : IFileHasher
    {
        private readonly IMonitorFactory _monitorFactory;
        private readonly IProgressFactory _progressFactory;

        public DefaultFileHasher(
            IMonitorFactory monitorFactory,
            IProgressFactory progressFactory)
        {
            _monitorFactory = monitorFactory;
            _progressFactory = progressFactory;
        }

        public async Task<string> ComputeHashAsync(FileInfo package)
        {
            // Compute the digest for the package first. We need it in both modes.
            var packagePath = package.FullName;
            string sha256;
            var digestPath = Path.Combine(Path.GetDirectoryName(packagePath) ?? Path.GetPathRoot(packagePath)!, Path.GetFileName(packagePath) + ".digest");
            if (!File.Exists(digestPath) || (new FileInfo(digestPath).LastWriteTimeUtc < new FileInfo(packagePath).LastWriteTimeUtc))
            {
                Console.WriteLine("hashing package to obtain sha256...");
                using (var hasher = SHA256.Create())
                {
                    using (var stream = new FileStream(packagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        var consoleWidth = 0;
                        try
                        {
                            consoleWidth = Console.BufferWidth;
                        }
                        catch
                        {
                        }

                        var cts = new CancellationTokenSource();
                        var progress = _progressFactory.CreateProgressForStream(stream);
                        var monitorTask = Task.Run(async () =>
                        {
                            var monitor = _monitorFactory.CreateByteBasedMonitor();
                            await monitor.MonitorAsync(
                                progress,
                                null,
                                (message, count) =>
                                {
                                    if (consoleWidth != 0)
                                    {
                                        Console.Write($"\r{message}".PadRight(consoleWidth));
                                    }
                                    else
                                    {
                                        if (count % 50 == 0)
                                        {
                                            Console.WriteLine(message);
                                        }
                                    }
                                },
                                cts.Token).ConfigureAwait(false);
                        });

                        var shaBytes = await hasher.ComputeHashAsync(stream).ConfigureAwait(false);
                        sha256 = "sha256:" + Hashing.Hash.Sha256AsHexString(shaBytes);

                        cts.Cancel();
                        try
                        {
                            await monitorTask.ConfigureAwait(false);
                        }
                        catch (TaskCanceledException) { }

                        if (consoleWidth != 0)
                        {
                            Console.WriteLine();
                        }
                    }
                }
                await File.WriteAllTextAsync(digestPath, sha256).ConfigureAwait(false);
            }
            else
            {
                Console.WriteLine("reading digest from existing digest file");
                sha256 = (await File.ReadAllTextAsync(digestPath).ConfigureAwait(false)).Trim();
            }
            return sha256;
        }
    }
}
