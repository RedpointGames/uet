namespace Redpoint.Uet.Workspace.Tests
{
    using Redpoint.Uet.Workspace.ParallelCopy;
    using static Redpoint.Uet.Workspace.ParallelCopy.DefaultParallelCopy;
    using System.Collections.Concurrent;

    public class ParallelCopyTests
    {
        [Fact]
        public async Task TestParallelCopyFileList()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "This test must run on Windows.");
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS"), "Required directory does not exist.");
            Directory.CreateDirectory(@"C:\Temp\TestParallelCopyFileList");

            var copyItems = new ConcurrentQueue<QueuedCopy>();
            var deleteItems = new ConcurrentQueue<QueuedDelete>();
            var copyStats = new CopyStats();

            await RecursiveScanAsync(
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

        [Fact]
        public async Task CanPerformCopy()
        {
            Assert.SkipUnless(Directory.Exists(@"C:\Work\internal\EOS_OSB\EOS_OSB\Plugins\EOS"), "Required directory does not exist.");
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

            await RecursiveScanAsync(
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