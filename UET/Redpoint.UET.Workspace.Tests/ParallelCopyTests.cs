namespace Redpoint.UET.Workspace.Tests
{
    using Redpoint.UET.Workspace.ParallelCopy;
    using static Redpoint.UET.Workspace.ParallelCopy.DefaultParallelCopy;
    using System.Collections.Concurrent;
    using System.Diagnostics;

    public class ParallelCopyTests
    {
        [SkippableFact]
        public async Task TestParallelCopyFileList()
        {
            Skip.IfNot(OperatingSystem.IsWindows());
            Skip.IfNot(Directory.Exists(@"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS"));
            Directory.CreateDirectory(@"C:\Temp\TestParallelCopyFileList");

            var parallelCopy = new DefaultParallelCopy();

            var copyItems = new ConcurrentQueue<QueuedCopy>();
            var deleteItems = new ConcurrentQueue<QueuedDelete>();
            var copyStats = new CopyStats();

            await parallelCopy.RecursiveScanAsync(
                new DirectoryInfo(@"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS"),
                new DirectoryInfo(@"C:\Temp\TestParallelCopyFileList"),
                copyItems,
                deleteItems,
                new CopyDescriptor
                {
                    SourcePath = @"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS",
                    DestinationPath = @"C:\Temp\TestParallelCopyFileList",
                    DirectoriesToRemoveExtraFilesUnder = new HashSet<string>
                    {
                        "Source",
                        "Content",
                        "Resources",
                        "Config"
                    },
                    ExcludePaths = new HashSet<string>
                    {
                        ".uet",
                        ".git",
                        "Engine/Saved/BuildGraph",
                    }
                },
                false,
                copyStats,
                CancellationToken.None);
        }

        [SkippableFact]
        public async Task CanPerformCopy()
        {
            Skip.IfNot(Directory.Exists(@"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS"));
            Directory.CreateDirectory(@"C:\Temp\CanPerformCopy");

            var parallelCopy = new DefaultParallelCopy();

            // Do the copy.
            var descriptor = new CopyDescriptor
            {
                SourcePath = @"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS",
                DestinationPath = @"C:\Temp\CanPerformCopy",
                DirectoriesToRemoveExtraFilesUnder = new HashSet<string>
                {
                    "Source",
                    "Content",
                    "Resources",
                    "Config"
                },
                ExcludePaths = new HashSet<string>
                {
                    ".uet",
                    ".git",
                    "Engine/Saved/BuildGraph",
                }
            };
            await parallelCopy.CopyAsync(
                descriptor,
                CancellationToken.None);

            // Make sure we have nothing to copy after the copy.
            var copyItems = new ConcurrentQueue<QueuedCopy>();
            var deleteItems = new ConcurrentQueue<QueuedDelete>();
            var copyStats = new CopyStats();

            await parallelCopy.RecursiveScanAsync(
                new DirectoryInfo(@"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS"),
                new DirectoryInfo(@"C:\Temp\CanPerformCopy"),
                copyItems,
                deleteItems,
                descriptor,
                false,
                copyStats,
                CancellationToken.None);

            Assert.Empty(copyItems);
            Assert.Empty(deleteItems);
        }
    }
}